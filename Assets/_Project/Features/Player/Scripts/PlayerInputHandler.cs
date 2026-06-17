using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    // プレイヤーの操作入力を受け取るスクリプト。
    // 追加操作があれば、随時ここに追加していく。
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("キャラクターの入力値")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool attack; // 左クリック
        public bool subAction; // 弓の構え 、 剣の防御
        public bool weaponNext;
        public bool weaponPrev;

        [Header("移動設定")]
        public bool analogMovement;

        [Header("マウスカーソル設定")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        private bool _attackPending = false;

        public bool HasAttackInput => _attackPending;

        public bool ConsumeAttack()
        {
            if (_attackPending)
            {
                _attackPending = false;
                return true;
            }
            return false;
        }

        public void OnMove(InputValue value)
        {
            MoveInput(value.Get<Vector2>());
        }

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
                LookInput(value.Get<Vector2>());
        }

        public void OnJump(InputValue value)
        {
            JumpInput(value.isPressed);
        }

        public void OnSprint(InputValue value)
        {
            SprintInput(value.isPressed);
        }

        public void OnSubAction(InputValue value)
        {
            SubActionInput(value.isPressed);
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed)
                AttackInput();
        }

        public void OnWeaponNext(InputValue value)
        {
            WeaponNextInput(value.isPressed);
        }

        public void OnWeaponPrev(InputValue value)
        {
            WeaponPrevInput(value.isPressed);
        }

        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            sprint = newSprintState;
        }

        public void SubActionInput(bool newSubActionState)
        {
            subAction = newSubActionState;
        }

        public void AttackInput()
        {
            _attackPending = true;
        }

        public void WeaponNextInput(bool newWeaponNextState)
        {
            weaponNext = newWeaponNextState;
        }

        public void WeaponPrevInput(bool newWeaponPrevState)
        {
            weaponPrev = newWeaponPrevState;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
