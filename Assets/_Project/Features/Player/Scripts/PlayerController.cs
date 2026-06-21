using UnityEngine;
using UnityEngine.InputSystem;

/* 注意：アニメーションは、AnimatorのNullチェックを利用し、キャラクターとカプセルの両方でコントローラーを介して呼び出されます。
 */

namespace CreativeAI.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("プレイヤー")]
        [Tooltip("キャラクターの移動速度 (m/s)")]
        public float MoveSpeed = 2.0f;

        [Tooltip("キャラクターのスプリント（ダッシュ）速度 (m/s)")]
        public float SprintSpeed = 5.335f;

        [Tooltip("キャラクターが移動方向に向き直る際の回転の滑らかさ（秒）")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("加速と減速の速さ")]
        public float SpeedChangeRate = 10.0f;

        public AudioSource AudioFootsteps;
        public AudioSource LandingAudio;
        public AudioSource AudioFoley;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;

        [Range(0, 1)]
        public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("プレイヤーのジャンプの高さ")]
        public float JumpHeight = 1.2f;

        [Tooltip("キャラクター固有の重力値。Unityエンジンのデフォルトは -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip(
            "再度ジャンプできるようになるまでに必要な時間。0fに設定するとすぐにジャンプ可能になります"
        )]
        public float JumpTimeout = 0.50f;

        [Tooltip("落下状態に移行するまでに必要な時間。階段を下りるときなどに便利です")]
        public float FallTimeout = 0.15f;

        [Header("プレイヤーの接地判定")]
        [Tooltip(
            "キャラクターが接地しているかどうか。CharacterControllerに組み込まれている接地判定(Grounded)とは異なります"
        )]
        public bool Grounded = true;

        [Tooltip("凹凸のある地面の判定調整用オフセット")]
        public float GroundedOffset = -0.14f;

        [Tooltip("接地判定の球体の半径。CharacterControllerの半径と一致させる必要があります")]
        public float GroundedRadius = 0.28f;

        [Tooltip("キャラクターが地面として認識するレイヤー")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip(
            "Cinemachine Virtual Cameraに設定される、カメラが追従するターゲットのゲームオブジェクト"
        )]
        public GameObject CinemachineCameraTarget;

        [Tooltip("カメラを上方向に動かせる最大角度（度）")]
        public float TopClamp = 70.0f;

        [Tooltip("カメラを下方向に動かせる最小角度（度）")]
        public float BottomClamp = -30.0f;

        [Tooltip(
            "カメラをオーバーライドする追加の角度。カメラ固定時にカメラ位置を微調整するのに便利です"
        )]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("すべての軸でカメラ位置を固定します")]
        public bool LockCameraPosition = false;

        [Header("アニメーションRig制御")]
        [Tooltip("背骨（ChestやSpine）のTransform")]
        public Transform SpineBone;

        // Cinemachine関連
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // プレイヤー関連
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // タイムアウト用の経過時間（DeltaTime）
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // アニメーションID
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        private PlayerInput _playerInput;
        private Animator _animator;
        private CharacterController _controller;
        private PlayerInputHandler _input;
        private GameObject _mainCamera;
        private const float _threshold = 0.01f;

        // 外部の武器スクリプトが操作するためのフラグ
        public bool CanMove = true;
        public bool CanChangeWeapon = true;
        public bool IsAiming = false; // エイム時用
        public bool IsFlinching = false; // 怯み中フラグ（PlayerFlinchHandler が管理）

        private bool IsCurrentDeviceMouse
        {
            get
            {
                return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _animator = GetComponent<Animator>();
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputHandler>();
            _playerInput = GetComponent<PlayerInput>();

            AssignAnimationIDs();

            // 開始時にタイムアウトをリセット
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();

            // エイムフラグが立っている時だけ背骨を曲げる
            if (IsAiming && SpineBone != null)
            {
                SpineBone.localEulerAngles += new Vector3(0, 0, _cinemachineTargetPitch);
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // オフセットを考慮して球体の位置を設定
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

            _animator.SetBool(_animIDGrounded, Grounded);
        }

        private void CameraRotation()
        {
            // 入力があり、かつカメラ位置が固定されていない場合
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                // マウス入力にはTime.deltaTimeを乗算しない
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // 回転角度をクランプして、値が360度以内に制限されるようにする
            _cinemachineTargetYaw = ClampAngle(
                _cinemachineTargetYaw,
                float.MinValue,
                float.MaxValue
            );
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachineはこのターゲットを追従する
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw,
                0.0f
            );
        }

        private void Move()
        {
            if (!CanMove)
            {
                _speed = 0f;
                _animationBlend = 0f;
                _controller.Move(new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
                return;
            }
            // スプリントは攻撃中・防御中・エイム中は禁止
            bool canSprint = _input.sprint && !IsAiming;

            // 移動速度、スプリント速度、スプリントが押されているかどうかに基づいて目標速度を設定
            float targetSpeed = canSprint ? SprintSpeed : MoveSpeed;

            // 削除、置換、またはイテレーションが容易なように設計されたシンプルな加速・減速処理

            // 注意：Vector2の == 演算子は近似値を使用するため、浮動小数点エラーが発生しにくく、magnitudeよりも処理が軽量です
            // 移動を停止するのは「剣の攻撃中・ダッシュ中」のみ。
            if (_input.move == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            // プレイヤーの現在の水平方向の速度への参照
            float currentHorizontalSpeed = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z
            ).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // 目標速度に向けて加速または減速
            if (
                currentHorizontalSpeed < targetSpeed - speedOffset
                || currentHorizontalSpeed > targetSpeed + speedOffset
            )
            {
                // 線形ではなくカーブした結果を作成し、より自然な速度変化を与える
                // 注意：LerpのTはクランプされるため、速度をクランプする必要はない
                _speed = Mathf.Lerp(
                    currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate
                );

                // 速度を小数点以下3桁に丸める
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

            // 入力方向を正規化
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            float targetDirectionAngle = _targetRotation;

            if (IsAiming)
            {
                // エイム中（弓装備時）：体の向きを強制的にカメラの正面（Y軸）に固定
                _targetRotation = _mainCamera.transform.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0.0f, _targetRotation, 0.0f);

                // 移動方向の計算（入力がある場合のみ）
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
                // 通常時：入力方向へ体を向ける（既存の処理）
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

            // 最終的な移動方向の決定
            Vector3 targetDirection =
                Quaternion.Euler(0.0f, targetDirectionAngle, 0.0f) * Vector3.forward;

            // プレイヤーを移動させる
            _controller.Move(
                targetDirection.normalized * (_speed * Time.deltaTime)
                    + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime
            );

            _animator.SetFloat(_animIDSpeed, _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            _animator.SetBool("Aim", IsAiming); // 弓装備時のみ Aim アニメーションを有効化
            _animator.SetFloat("StrafeX", _input.move.x);
            _animator.SetFloat("StrafeZ", _input.move.y);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // 落下タイムアウトタイマーをリセット
                _fallTimeoutDelta = FallTimeout;

                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);

                // 接地時に垂直速度が無限に下がり続けるのを防ぐ
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // ジャンプ
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // H * -2 * G の平方根 = 目的の高さに達するために必要な垂直速度
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    _animator.SetBool(_animIDJump, true);
                }

                // ジャンプタイムアウト
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // ジャンプタイムアウトタイマーをリセット
                _jumpTimeoutDelta = JumpTimeout;

                // 落下タイムアウト
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // キャラクターを使用している場合はAnimatorを更新
                    _animator.SetBool(_animIDFreeFall, true);
                }

                // 接地していない場合はジャンプしない
                _input.jump = false;
            }

            // 終端速度未満であれば、時間の経過とともに重力を適用する（時間の経過とともに線形に加速させるため、Delta Timeを2回乗算する）
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f)
                lfAngle += 360f;
            if (lfAngle > 360f)
                lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded)
                Gizmos.color = transparentGreen;
            else
                Gizmos.color = transparentRed;

            // 選択時、接地コライダーの位置と半径に一致するギズモを描画する
            Gizmos.DrawSphere(
                new Vector3(
                    transform.position.x,
                    transform.position.y - GroundedOffset,
                    transform.position.z
                ),
                GroundedRadius
            );
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (AudioFootsteps != null)
                    AudioFootsteps.Play();
                if (AudioFoley != null)
                    AudioFoley.Play();
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (LandingAudio != null)
                    LandingAudio.Play();
            }
        }
    }
}
