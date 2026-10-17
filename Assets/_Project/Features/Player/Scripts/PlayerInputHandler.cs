using System;
using CreativeAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Input Systemからの入力イベントを受け取り、各フィールドに保持する。
    /// 攻撃入力はConsumeパターン（1回消費）を採用し、
    /// 武器コントローラー側が任意のタイミングで入力を読み取れるようにしている。
    /// 会話イベント中(<see cref="EventPlaybackService.BlocksPlayerControl"/>)は入力を捨てて操作不能にする。
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("キャラクターの入力値")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;
        public bool subAction; // 武器ごとに異なるアクション（剣：防御、弓：構え、鎌：投擲）に使用
        public bool weaponNext;
        public bool weaponPrev;

        [Header("移動設定")]
        public bool analogMovement;

        [Header("マウスカーソル設定")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        [Header("先行入力")]
        [Tooltip(
            "攻撃ボタンを押してから、この秒数以内に攻撃できる状態になれば攻撃が出る(先行入力の有効期限)。"
                + "期限を過ぎた入力は捨てる(怯み中に押した攻撃が、怯み明けに勝手に出るのを防ぐ)"
        )]
        [SerializeField]
        private float _attackBufferSeconds = 0.3f;

        private bool _attackPending = false;
        private float _attackPressedTime;

        public bool HasAttackInput => _attackPending;

        private bool IsAttackInputExpired => Time.time - _attackPressedTime > _attackBufferSeconds;

        private GameModeManager _gameMode;

        private void OnEnable()
        {
            EventPlaybackService.PlayingChanged += OnPlayingChanged;
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.OnModeChanged += OnModeChanged;
            OnControlStateChanged();
        }

        private void OnDisable()
        {
            EventPlaybackService.PlayingChanged -= OnPlayingChanged;
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            _gameMode = null;
        }

        private void OnPlayingChanged(bool _) => OnControlStateChanged();

        private void OnModeChanged(GameMode _) => OnControlStateChanged();

        // 操作不能になった瞬間に押しっぱなしの入力を捨てる(会話中に歩き続けたり、解除直後に攻撃が出たりしないように)。
        private void OnControlStateChanged()
        {
            if (EventPlaybackService.BlocksPlayerControl)
                ClearInputs();
        }

        private void ClearInputs()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            jump = false;
            sprint = false;
            subAction = false;
            weaponNext = false;
            weaponPrev = false;
            _attackPending = false;
        }

        private static bool Blocked => EventPlaybackService.BlocksPlayerControl;

        /// <summary>
        /// 攻撃入力を消費する。1回の入力に対して1回だけtrueを返す。
        /// 武器コントローラー側で攻撃可能なタイミングまで入力を保持し、
        /// 準備ができた時点で消費する設計。有効期限を過ぎた入力は false を返して捨てる。
        /// </summary>
        public bool ConsumeAttack()
        {
            if (!_attackPending)
                return false;

            _attackPending = false;
            return !IsAttackInputExpired;
        }

        /// <summary>保持している入力値をすべて消す(死亡時など、入力を無効化する直前に呼ぶ)。</summary>
        public void ClearAllInput()
        {
            move = Vector2.zero;
            look = Vector2.zero;
            jump = false;
            sprint = false;
            subAction = false;
            weaponNext = false;
            weaponPrev = false;
            _attackPending = false;
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

        /// <summary>
        /// subActionが新たに「押された」瞬間（trueへの変化）にのみ発火するイベント。
        /// PanicDetectorが購読し、剣の防御連打の頻度を計測するために使用する。
        /// </summary>
        public event Action OnSubActionPressed;

        public void OnSubAction(InputValue value)
        {
            if (Blocked)
                return;
            bool pressed = value.isPressed;
            if (pressed && !subAction)
            {
                OnSubActionPressed?.Invoke();
            }
            SubActionInput(pressed);
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
            if (Blocked)
                return;
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            if (Blocked)
                return;
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            if (Blocked)
                return;
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            if (Blocked)
                return;
            sprint = newSprintState;
        }

        public void SubActionInput(bool newSubActionState)
        {
            if (Blocked)
                return;
            subAction = newSubActionState;
        }

        /// <summary>
        /// 攻撃ボタンが押された瞬間に発火するイベント。
        /// PanicDetectorが購読し、ボスの攻撃中にプレイヤーが何か操作したか（硬直していないか）を判定するために使用する。
        /// </summary>
        public event Action OnAttackPressed;

        public void AttackInput()
        {
            if (Blocked)
                return;
            _attackPending = true;
            _attackPressedTime = Time.time;
            OnAttackPressed?.Invoke();
        }

        public void WeaponNextInput(bool newWeaponNextState)
        {
            if (Blocked)
                return;
            weaponNext = newWeaponNextState;
        }

        public void WeaponPrevInput(bool newWeaponPrevState)
        {
            if (Blocked)
                return;
            weaponPrev = newWeaponPrevState;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
