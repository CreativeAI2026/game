using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの移動・ジャンプ・接地判定と、行動制限・状態フラグの保持を担当する。
    /// エイム中は体の向きをカメラに固定し、8方向ストレイフ移動に切り替える。
    /// カメラの回転は <see cref="PlayerCameraController"/>、エイム時の背骨の曲げは <see cref="PlayerAimSpineBend"/> が担う。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("プレイヤー")]
        [Tooltip("キャラクターの移動速度 (m/s)")]
        public float MoveSpeed = 2.0f;

        [Tooltip("キャラクターのスプリント（ダッシュ）速度 (m/s)")]
        public float SprintSpeed = 5.335f;

        [Tooltip(
            "弓を構えている間の移動速度 (m/s)。構え歩きクリップの歩幅(前後約1.1、横約1.4)に合わせ、足が滑らない速さにする"
        )]
        public float AimMoveSpeed = 1.5f;

        [Tooltip("キャラクターが移動方向に向き直る際の回転の滑らかさ（秒）")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("弓を構えた時に、体をカメラの向きへ振り向かせる滑らかさ（秒）。0 で即座に向く")]
        [Range(0.0f, 0.3f)]
        public float AimRotationSmoothTime = 0.04f;

        [Tooltip("加速と減速の速さ")]
        public float SpeedChangeRate = 10.0f;

        [Tooltip("キャラクター固有の重力値。Unityエンジンのデフォルトは -9.81f")]
        public float Gravity = -15.0f;

        [Tooltip("落下状態に移行するまでに必要な時間。階段を下りるときなどに使う。")]
        public float FallTimeout = 0.15f;

        [Header("プレイヤーの接地判定")]
        [Tooltip("凹凸のある地面の判定調整用オフセット")]
        public float GroundedOffset = -0.14f;

        [Tooltip("接地判定の球体の半径。CharacterControllerの半径と一致させる必要がある")]
        public float GroundedRadius = 0.28f;

        [Tooltip("キャラクターが地面として認識するレイヤー")]
        public LayerMask GroundLayers;

        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private float _fallTimeoutDelta;

        private Animator _animator;
        private CharacterController _controller;
        private PlayerInputHandler _input;
        private GameObject _mainCamera;

        /// <summary>接地しているか(CharacterController 組み込みの isGrounded とは別に、球判定で求める)。</summary>
        public bool Grounded { get; private set; } = true;

        /// <summary>行動制限の要求。制限を掛ける側は Set(this, …) / Release(this) で自分の分だけを出し入れする。</summary>
        public PlayerActionLocks Locks { get; } = new PlayerActionLocks();

        public bool CanMove => !Locks.IsLocked(PlayerLock.Move);
        public bool CanChangeWeapon => !Locks.IsLocked(PlayerLock.ChangeWeapon);

        // 実行時の状態。シーンに保存されないようプロパティにしている(書き手はそれぞれ1か所)
        /// <summary>弓を構えているか(書き手: 弓)。</summary>
        public bool IsAiming { get; set; }

        /// <summary>怯み中か(書き手: PlayerFlinchHandler)。</summary>
        public bool IsFlinching { get; set; }

        /// <summary>敵に掴まれているか(書き手: 掴み攻撃を行う敵)。</summary>
        public bool IsGrabbed { get; set; }

        /// <summary>
        /// 入力から決まる、今出そうとしている水平移動速度（m/s。歩き/走り × スティックの倒し具合）。
        /// 移動できない時・入力が無い時は 0。実際の速度と比べて「入力しているのに動けていない」を判定するのに使う。
        /// </summary>
        public float DesiredSpeed { get; private set; }

        private void Awake()
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            Assert.IsNotNull(_mainCamera, $"{name}: MainCamera タグのカメラが見つかりません");

            _animator = PlayerModel.Of(this).Animator;
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputHandler>();

            // UI・カメラなどシーン側がプレイヤーを受け取れるよう登録する
            PlayerRegistry.Register(this);
        }

        private void OnDestroy()
        {
            PlayerRegistry.Unregister(this);
        }

        private void Start()
        {
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            FallGravity();
            GroundedCheck();
            Move();
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(
                transform.position.x,
                transform.position.y - GroundedOffset,
                transform.position.z
            );
            Grounded = Physics.CheckSphere(
                spherePosition,
                GroundedRadius,
                GroundLayers,
                QueryTriggerInteraction.Ignore
            );

            _animator.SetBool(PlayerAnimatorParams.Grounded, Grounded);
        }

        private void Move()
        {
            // 移動できない間(怯み等)もエイム解除を Animator に伝えるため、移動可否より先に反映する
            _animator.SetBool(PlayerAnimatorParams.Aim, IsAiming);

            if (!CanMove)
            {
                DesiredSpeed = 0f;
                _speed = 0f;
                _animationBlend = 0f;
                _controller.Move(new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
                _animator.SetFloat(PlayerAnimatorParams.Speed, 0f);
                _animator.SetFloat(PlayerAnimatorParams.MotionSpeed, 0f);
                return;
            }

            bool canSprint = _input.sprint && !IsAiming;

            float targetSpeed =
                IsAiming ? AimMoveSpeed
                : canSprint ? SprintSpeed
                : MoveSpeed;

            // Vector2の == 演算子は内部で近似値比較を行うため、微小な入力による意図しない移動を防げる
            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            float currentHorizontalSpeed = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z
            ).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;
            DesiredSpeed = targetSpeed * inputMagnitude;

            if (
                currentHorizontalSpeed < targetSpeed - speedOffset
                || currentHorizontalSpeed > targetSpeed + speedOffset
            )
            {
                // Lerpによる非線形補間で、急加速・急減速を避けて自然な速度変化を実現する
                _speed = Mathf.Lerp(
                    currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate
                );

                // アニメーションのブレンドツリーに渡す値の精度を安定させるため、小数点以下3桁に丸める
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(
                _animationBlend,
                targetSpeed,
                Time.deltaTime * SpeedChangeRate
            );
            if (_animationBlend < 0.01f)
                _animationBlend = 0f;

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            float targetDirectionAngle = _targetRotation;

            if (IsAiming)
            {
                // エイム中は体をカメラの向きに合わせ、移動はストレイフ（横移動）として処理する。
                // 構えた瞬間に最大180度回るとポーズが飛んで見えるため、ごく短い時間で振り向かせる
                // (矢はカメラ中央へ飛ぶので、体の向きが一瞬遅れても狙いは変わらない)
                _targetRotation = _mainCamera.transform.eulerAngles.y;
                float aimRotation = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    _targetRotation,
                    ref _rotationVelocity,
                    AimRotationSmoothTime
                );
                transform.rotation = Quaternion.Euler(0.0f, aimRotation, 0.0f);

                if (_input.move != Vector2.zero)
                {
                    targetDirectionAngle =
                        Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                        + _mainCamera.transform.eulerAngles.y;
                }
                else
                {
                    targetDirectionAngle = _targetRotation;
                }
            }
            else
            {
                if (_input.move != Vector2.zero)
                {
                    _targetRotation =
                        Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg
                        + _mainCamera.transform.eulerAngles.y;
                    float rotation = Mathf.SmoothDampAngle(
                        transform.eulerAngles.y,
                        _targetRotation,
                        ref _rotationVelocity,
                        RotationSmoothTime
                    );
                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }
                targetDirectionAngle = _targetRotation;
            }

            Vector3 targetDirection =
                Quaternion.Euler(0.0f, targetDirectionAngle, 0.0f) * Vector3.forward;

            _controller.Move(
                targetDirection.normalized * (_speed * Time.deltaTime)
                    + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime
            );

            _animator.SetFloat(PlayerAnimatorParams.Speed, _animationBlend);
            _animator.SetFloat(PlayerAnimatorParams.MotionSpeed, inputMagnitude);
            _animator.SetFloat(PlayerAnimatorParams.StrafeX, _input.move.x);
            _animator.SetFloat(PlayerAnimatorParams.StrafeZ, _input.move.y);
        }

        private void FallGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                _animator.SetBool(PlayerAnimatorParams.FreeFall, false);

                // 接地中は速度を-2fに保つ。0fだとCheckSphereとの判定タイミング次第で
                // 接地→浮遊を繰り返すジッターが発生するため、わずかに地面に押し付ける
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                // 段差を下りる程度の短い浮きでは落下モーションにしない
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    _animator.SetBool(PlayerAnimatorParams.FreeFall, true);
                }
            }

            // 終端速度未満であれば重力を適用。deltaTimeを2回乗算するのは、等加速度運動（v += a*dt）の離散化のため
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded)
                Gizmos.color = transparentGreen;
            else
                Gizmos.color = transparentRed;

            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z
                ),
                GroundedRadius
            );
        }
    }
}
