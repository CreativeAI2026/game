using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 本番の PlayerRig(モデル + PlayerController + アニメ + WeaponManager)が出来るまでの、
    /// マップの形を歩いて確かめるための仮リグ用コントローラ。カプセルとカメラを動かすだけ。
    /// 入力方式が Input System のみ(activeInputHandler=1)なので Keyboard/Mouse.current を直接読む。
    /// 本番リグが出来たら ResidentBootstrapConfig.playerRigPrefab を差し替え、この Prefab と
    /// スクリプトごと捨てる。本番の実装は Features/Player/Scripts/PlayerController.cs。
    ///
    /// 操作: WASD/矢印 = 移動、マウス = 視点、Shift = ダッシュ、Space = ジャンプ、Esc = カーソル解放。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DummyPlayerController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("カメラをぶら下げているピボット。上下の視点はこれを回す")]
        private Transform _cameraPivot;

        [SerializeField]
        private float _moveSpeed = 6f;

        [SerializeField]
        private float _sprintSpeed = 12f;

        [SerializeField]
        private float _jumpHeight = 1.2f;

        [SerializeField]
        private float _gravity = -15f;

        [SerializeField]
        private float _lookSensitivity = 0.12f;

        private CharacterController _controller;
        private float _yaw;
        private float _pitch = 15f;
        private float _verticalSpeed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
        }

        private void OnEnable() => SetCursorLocked(true);

        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return; // キーボード無し(自動テスト等)では何もしない

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            Look(Mouse.current);
            Move(keyboard);
        }

        private void Look(Mouse mouse)
        {
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * _lookSensitivity, -70f, 70f);
            }

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraPivot != null)
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void Move(Keyboard keyboard)
        {
            float x = Axis(
                keyboard.dKey,
                keyboard.rightArrowKey,
                keyboard.aKey,
                keyboard.leftArrowKey
            );
            float z = Axis(
                keyboard.wKey,
                keyboard.upArrowKey,
                keyboard.sKey,
                keyboard.downArrowKey
            );

            Vector3 move = transform.right * x + transform.forward * z;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            if (_controller.isGrounded)
            {
                _verticalSpeed = -1f; // 斜面に張り付かせるための下向きの当て
                if (keyboard.spaceKey.wasPressedThisFrame)
                    _verticalSpeed = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
            else
            {
                _verticalSpeed += _gravity * Time.deltaTime;
            }

            float speed = keyboard.leftShiftKey.isPressed ? _sprintSpeed : _moveSpeed;
            _controller.Move((move * speed + Vector3.up * _verticalSpeed) * Time.deltaTime);
        }

        private static float Axis(
            KeyControl plusA,
            KeyControl plusB,
            KeyControl minusA,
            KeyControl minusB
        ) =>
            (plusA.isPressed || plusB.isPressed ? 1f : 0f)
            - (minusA.isPressed || minusB.isPressed ? 1f : 0f);

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
