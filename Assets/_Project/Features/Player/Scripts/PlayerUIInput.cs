using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 画面(キャラ / インベ / マップ)を開閉する入力の受け口と、UI 操作が必要な場面のアクションマップ切替を担う。
    /// 操作で開くUIの表示中(<see cref="GameModeManager.IsMenuOpen"/>)と会話中(<see cref="GameMode.Dialogue"/>)は
    /// PlayerInput を UI マップへ、どちらも終わったら Player マップへ戻す。
    /// C / I / M は両マップに同名のアクションがあるので、どちらのマップでも同じメッセージ(OnCharacter / OnInventory / OnMap)で届く。
    /// どのパネルを開くかは UI 側(HudIconBarView)が決める(プレイヤーは UI を知らない)。
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerUIInput : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string UIMapName = "UI";

        /// <summary>キャラ画面の開閉キー(C)が押された。</summary>
        public event Action CharacterPressed;

        /// <summary>インベ画面の開閉キー(I / ＋)が押された。</summary>
        public event Action InventoryPressed;

        /// <summary>セーブ画面の開閉キー(十字下)が押された。</summary>
        public event Action SavePressed;

        /// <summary>全画面マップの開閉キー(M)が押された。</summary>
        public event Action MapPressed;

        /// <summary>ミニマップの拡大・縮小の切り替えキー(Z / 右スティック押し込み)が押された。Player マップのときだけ届く。</summary>
        public event Action MinimapZoomPressed;

        /// <summary>UI マップの Cancel(Esc / B 等)が押された。開いている画面を閉じるのに使う。</summary>
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
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
            ApplyActionMap();
        }

        private void OnDisable()
        {
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
            _gameMode = null;
        }

        // 会話に入る・会話の battle ステップで操作が戻る、のどちらもモードの変化で判定し直す。
        private void OnModeChanged() => ApplyActionMap();

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

        public void OnSave(InputValue value)
        {
            if (value.isPressed)
                SavePressed?.Invoke();
        }

        public void OnMap(InputValue value)
        {
            if (value.isPressed)
                MapPressed?.Invoke();
        }

        public void OnMinimapZoom(InputValue value)
        {
            if (value.isPressed)
                MinimapZoomPressed?.Invoke();
        }

        public void OnCancel(InputValue value)
        {
            if (value.isPressed)
                CancelPressed?.Invoke();
        }

        // UI 表示中に Player マップを切ることで、移動・攻撃などが UI 操作中に一切届かないようにする。
        private void ApplyActionMap()
        {
            bool needsUi = GameModeManager.MenuOpen || GameModeManager.Current == GameMode.Dialogue;
            string mapName = needsUi ? UIMapName : PlayerMapName;
            if (_playerInput.currentActionMap?.name == mapName)
                return;
            _playerInput.SwitchCurrentActionMap(mapName);
        }
    }
}
