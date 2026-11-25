using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面右上の円形ナビ(キャラ / インベ / セーブ)。HP の HUD とは別 Canvas(HP は入力を受けず頻繁に更新されるため)。
    /// ボタンで <see cref="UIRouter.Toggle"/> を呼び、<see cref="GameModeManager.OnModeChanged"/> を購読して Field=表示 / Battle=非表示 を自分で切り替える。
    /// 表示中はキーでも開ける(C=キャラ / I=インベ / M=全画面マップ、UI マップの Cancel で閉じる)。キー入力は PlayerInputActions のアクションで、
    /// プレイヤーの <see cref="PlayerUIInput"/> から受け取る(プレイヤーは実行時生成なので <see cref="PlayerRegistry"/> 経由)。
    /// 隠れるとき(戦闘・会話の開始)は開いているパネルも閉じる。
    /// </summary>
    public sealed class HudIconBarView : MonoBehaviour
    {
        [SerializeField]
        private UIRouter _router;

        [SerializeField]
        private Button _characterButton;

        [SerializeField]
        private Button _inventoryButton;

        [SerializeField]
        private Button _saveButton;

        [Header("モード連動で出し入れする自分の CanvasGroup")]
        [SerializeField]
        private CanvasGroup _group;

        private GameModeManager _gameMode;
        private PlayerUIInput _uiInput;
        private bool _shown;

        private void Awake()
        {
            if (_group == null)
                _group = ResidentUIVisibility.GetOrAddGroup(gameObject);
            if (_router == null)
                _router = GetComponentInParent<UIRouter>(true); // 親 UIRoot の排他ルータ

            Bind(_characterButton, UIRouter.UIId.Character);
            Bind(_inventoryButton, UIRouter.UIId.Inventory);
            Bind(_saveButton, UIRouter.UIId.Save);
        }

        private void OnEnable()
        {
            // 常駐生成順ではマネージャが先に居るので Instance を取れる。無ければ Field 既定で表示。
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.OnModeChanged += OnModeChanged;
            // 会話イベント中も隠す(セーブ/インベを開けなくする)。
            EventPlaybackService.PlayingChanged += OnPlaybackChanged;
            PlayerRegistry.Changed += BindPlayer;
            BindPlayer(PlayerRegistry.Current);
            ApplyCurrent();
        }

        private void OnDisable()
        {
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            EventPlaybackService.PlayingChanged -= OnPlaybackChanged;
            PlayerRegistry.Changed -= BindPlayer;
            BindPlayer(null);
        }

        /// <summary>キー入力の購読先を切り替える(未生成・破棄時は null)。</summary>
        private void BindPlayer(PlayerController player)
        {
            if (_uiInput != null)
            {
                _uiInput.CharacterPressed -= OnCharacterPressed;
                _uiInput.InventoryPressed -= OnInventoryPressed;
                _uiInput.SavePressed -= OnSavePressed;
                _uiInput.MapPressed -= OnMapPressed;
                _uiInput.CancelPressed -= OnCancelPressed;
            }

            _uiInput = player != null ? player.GetComponent<PlayerUIInput>() : null;

            if (_uiInput != null)
            {
                _uiInput.CharacterPressed += OnCharacterPressed;
                _uiInput.InventoryPressed += OnInventoryPressed;
                _uiInput.SavePressed += OnSavePressed;
                _uiInput.MapPressed += OnMapPressed;
                _uiInput.CancelPressed += OnCancelPressed;
            }
        }

        private void Bind(Button button, UIRouter.UIId id)
        {
            if (button == null || _router == null)
                return;
            button.onClick.AddListener(() => _router.Toggle(id));
        }

        private void OnCharacterPressed() => ToggleByKey(UIRouter.UIId.Character);

        private void OnInventoryPressed() => ToggleByKey(UIRouter.UIId.Inventory);

        private void OnSavePressed() => ToggleByKey(UIRouter.UIId.Save);

        private void OnMapPressed() => ToggleByKey(UIRouter.UIId.Map);

        private void ToggleByKey(UIRouter.UIId id)
        {
            if (_shown && _router != null)
                _router.Toggle(id);
        }

        private void OnCancelPressed()
        {
            if (_shown && _router != null && _router.IsAnyPanelOpen)
                _router.CloseAll();
        }

        private void OnModeChanged(GameMode mode) => ApplyCurrent();

        private void OnPlaybackChanged(bool playing) => ApplyCurrent();

        private void ApplyCurrent()
        {
            var mode = _gameMode != null ? _gameMode.CurrentMode : GameMode.Field;
            Apply(mode);
        }

        /// <summary>
        /// Field かつ会話イベント中でないときだけ表示。Battle・会話中は非表示にしてセーブ等を開けなくする。
        /// GameObject ごと SetActive すると自分が止まって購読を失うため、
        /// CanvasGroup で隠すだけにして自身は生かし続ける。
        /// </summary>
        private void Apply(GameMode mode)
        {
            bool show = mode == GameMode.Field && !EventPlaybackService.IsPlaying;
            _shown = show;
            if (!show && _router != null && _router.IsAnyPanelOpen)
                _router.CloseAll();
            if (_group != null)
                _group.SetShown(show);
        }
    }
}
