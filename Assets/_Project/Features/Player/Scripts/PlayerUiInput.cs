using System;
using CreativeAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 画面(キャラ / インベ)を開閉する入力の受け口と、UI 操作が必要な場面のアクションマップ切替を担う。
    /// 操作で開くUIの表示中(<see cref="UiPanelService.IsOpen"/>)と会話イベント中(<see cref="EventPlaybackService.BlocksPlayerControl"/>)は
    /// PlayerInput を UI マップへ、どちらも終わったら Player マップへ戻す。
    /// C / I は両マップに同名のアクションがあるので、どちらのマップでも同じメッセージ(OnCharacter / OnInventory)で届く。
    /// どのパネルを開くかは UI 側(HudIconBar)が決める(プレイヤーは UI を知らない)。
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerUiInput : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string UiMapName = "UI";

        /// <summary>キャラ画面の開閉キー(C)が押された。</summary>
        public event Action CharacterPressed;

        /// <summary>インベ画面の開閉キー(I)が押された。</summary>
        public event Action InventoryPressed;

        /// <summary>UI マップの Cancel(Esc 等)が押された。開いている画面を閉じるのに使う。</summary>
        public event Action CancelPressed;

        private PlayerInput _playerInput;
        private InputActionAsset _ownActions;
        private GameModeManager _gameMode;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            // PlayerInputActions はプロジェクト全体のアクションでもあり、EventSystem の UI 入力モジュールも同じ実体を使う。
            // 共有したままマップを切り替えると UI マップ(Point / Click)まで止まり UI を操作できなくなるので、プレイヤー用に複製する。
            _ownActions = Instantiate(_playerInput.actions);
            _playerInput.actions = _ownActions;
        }

        private void OnDestroy() => Destroy(_ownActions);

        private void OnEnable()
        {
            UiPanelService.OpenChanged += OnControlStateChanged;
            EventPlaybackService.PlayingChanged += OnControlStateChanged;
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.OnModeChanged += OnModeChanged;
            ApplyActionMap();
        }

        private void OnDisable()
        {
            UiPanelService.OpenChanged -= OnControlStateChanged;
            EventPlaybackService.PlayingChanged -= OnControlStateChanged;
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            _gameMode = null;
        }

        private void OnControlStateChanged(bool _) => ApplyActionMap();

        // 会話イベント中の battle ステップは Battle モードになり操作を戻すので、モード変化でも判定し直す。
        private void OnModeChanged(GameMode _) => ApplyActionMap();

        public void OnCharacter(InputValue value)
        {
            if (value.isPressed)
                CharacterPressed?.Invoke();
        }

        public void OnInventory(InputValue value)
        {
            if (value.isPressed)
                InventoryPressed?.Invoke();
        }

        public void OnCancel(InputValue value)
        {
            if (value.isPressed)
                CancelPressed?.Invoke();
        }

        // UI 表示中に Player マップを切ることで、移動・攻撃などが UI 操作中に一切届かないようにする。
        private void ApplyActionMap()
        {
            bool needsUi = UiPanelService.IsOpen || EventPlaybackService.BlocksPlayerControl;
            string mapName = needsUi ? UiMapName : PlayerMapName;
            if (_playerInput.currentActionMap?.name == mapName)
                return;
            _playerInput.SwitchCurrentActionMap(mapName);
        }
    }
}
