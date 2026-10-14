using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 視点入力でカメラの追従ターゲット(Cinemachine の Tracking Target)を回転させる。
    /// PlayerController から分離した、カメラの向きだけの担当。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerCameraController : MonoBehaviour
    {
        // これ未満の視点入力は無視する(スティックの遊び・微小なマウス移動でカメラが揺れないように)
        private const float LookInputThreshold = 0.01f;

        [Tooltip(
            "Cinemachine Camera の Tracking Target に設定する、プレイヤー配下の空オブジェクト(PlayerCameraRoot)"
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

        private PlayerController _playerController;
        private PlayerInputHandler _input;
        private PlayerInput _playerInput;
        private float _yaw;
        private float _pitch;

        /// <summary>現在のカメラの上下角(度)。正で下向き。</summary>
        public float Pitch => _pitch;

        private bool IsCurrentDeviceMouse => _playerInput.currentControlScheme == "KeyboardMouse";

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInputHandler>();
            _playerInput = GetComponent<PlayerInput>();
            Assert.IsNotNull(
                CinemachineCameraTarget,
                $"{name}: CinemachineCameraTarget が未設定です"
            );
        }

        private void Start()
        {
            _yaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        }

        private void LateUpdate()
        {
            // 掴み中はカメラ制御を vcam に完全に委ねる。
            // CinemachineCameraTarget への強制書き込みを続けると、vcam の Follow/LookAt 設定や
            // Animation Rigging の IK と競合して首の骨が暴れる原因になる。
            if (_playerController.IsGrabbed)
                return;

            if (_input.look.sqrMagnitude >= LookInputThreshold && !LockCameraPosition)
            {
                // マウスの移動量は「前フレームからのピクセル移動量（Delta）」であり既にフレーム間の差分を含んでいるため、
                // 重複してTime.deltaTimeを乗算するとフレームレート依存のバグが生じる。
                // 一方、ゲームパッドのスティックは「傾き（速度）」のため乗算が必要。
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _yaw += _input.look.x * deltaTimeMultiplier;
                _pitch += _input.look.y * deltaTimeMultiplier;
            }

            // 累積角度が±360を超えるとfloat精度が劣化するため、範囲内に収める
            _yaw = ClampAngle(_yaw, float.MinValue, float.MaxValue);
            _pitch = ClampAngle(_pitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _pitch + CameraAngleOverride,
                _yaw,
                0.0f
            );
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f)
                angle += 360f;
            if (angle > 360f)
                angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }
    }
}
