using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの移動・ジャンプ・カメラ回転・接地判定を統合管理するコンポーネント。
    /// エイム中は体の向きをカメラに固定し、8方向ストレイフ移動に切り替える。
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
            "再度ジャンプできるようになるまでに必要な時間。0fに設定するとすぐにジャンプ可能になる。"
        )]
        public float JumpTimeout = 0.50f;

        [Tooltip("落下状態に移行するまでに必要な時間。階段を下りるときなどに使う。")]
        public float FallTimeout = 0.15f;

        [Header("プレイヤーの接地判定")]
        [Tooltip(
            "キャラクターが接地しているかどうか。CharacterControllerに組み込まれている接地判定(Grounded)とは異なる。"
        )]
        public bool Grounded = true;

        [Tooltip("凹凸のある地面の判定調整用オフセット")]
        public float GroundedOffset = -0.14f;

        [Tooltip("接地判定の球体の半径。CharacterControllerの半径と一致させる必要がある")]
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
            "カメラをオーバーライドする追加の角度。カメラ固定時にカメラ位置を微調整するための変数"
        )]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("すべての軸でカメラ位置を固定する")]
        public bool LockCameraPosition = false;

        [Header("アニメーションRig制御")]
        [Tooltip("背骨（ChestやSpine）のTransform")]
        public Transform SpineBone;

        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

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

        public bool CanMove = true;
        public bool CanChangeWeapon = true;
        public bool IsAiming = false;
        public bool IsFlinching = false;

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

            // エイム中に背骨をカメラのピッチ角に連動させることで、上下方向のエイムを体の姿勢に反映する
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
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                // マウスの移動量は「前フレームからのピクセル移動量（Delta）」であり既にフレーム間の差分を含んでいるため、
                // 重複してTime.deltaTimeを乗算するとフレームレート依存のバグが生じる。
                // 一方、ゲームパッドのスティックは「傾き（速度）」のため乗算が必要。
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // 累積角度が±360を超えるとfloat精度が劣化するため、範囲内に収める
            _cinemachineTargetYaw = ClampAngle(
                _cinemachineTargetYaw,
                float.MinValue,
                float.MaxValue
            );
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

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

            bool canSprint = _input.sprint && !IsAiming;

            float targetSpeed = canSprint ? SprintSpeed : MoveSpeed;

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
                // エイム中はカメラの向きに体を固定し、移動はストレイフ（横移動）として処理する
                _targetRotation = _mainCamera.transform.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0.0f, _targetRotation, 0.0f);

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

            _animator.SetFloat(_animIDSpeed, _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            _animator.SetBool("Aim", IsAiming);
            _animator.SetFloat("StrafeX", _input.move.x);
            _animator.SetFloat("StrafeZ", _input.move.y);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);

                // 接地中は速度を-2fに保つ。0fだとCheckSphereとの判定タイミング次第で
                // 接地→浮遊を繰り返すジッターが発生するため、わずかに地面に押し付ける
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // 運動方程式 v = √(2gh) から、指定した高さに到達するための初速を算出
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    _animator.SetBool(_animIDJump, true);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    _animator.SetBool(_animIDFreeFall, true);
                }

                // 空中でのジャンプ入力を無効化する（二段ジャンプ防止）
                _input.jump = false;
            }

            // 終端速度未満であれば重力を適用。deltaTimeを2回乗算するのは、等加速度運動（v += a*dt）の離散化のため
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
