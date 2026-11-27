using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面右上の円形ナビ(キャラ / インベ / セーブ)。HP の HUD とは別 Canvas(HP は入力を受けず頻繁に更新されるため)。
    /// ボタンで <see cref="UIManager.Toggle"/> を呼び、<see cref="GameModeManager.Changed"/> を購読して Field=表示 / 会話・戦闘=非表示 を自分で切り替える。
    /// 表示中はキーでも開ける(C=キャラ / I=インベ / M=全画面マップ、UI マップの Cancel で閉じる)。キー入力は PlayerInputActions のアクションで、
    /// プレイヤーの <see cref="PlayerUIInput"/> から受け取る(プレイヤーは実行時生成なので <see cref="PlayerRegistry"/> 経由)。
    /// 隠れるとき(戦闘・会話の開始)は開いているパネルも閉じる。
    /// </summary>
    public sealed class HudIconBarView : MonoBehaviour
    {
        [SerializeField]
        private UIManager _ui;

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
            if (_ui == null)
                _ui = GetComponentInParent<UIManager>(true); // 親 UIRoot Prefab の UIManager

            Bind(_characterButton, UIManager.UIId.Character);
            Bind(_inventoryButton, UIManager.UIId.Inventory);
            Bind(_saveButton, UIManager.UIId.Save);
        }

        private void OnEnable()
        {
            // 常駐生成順ではマネージャが先に居るので Instance を取れる。無ければ Field 既定で表示。
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
            PlayerRegistry.Changed += BindPlayer;
            BindPlayer(PlayerRegistry.Current);
            Apply();
        }

        private void OnDisable()
        {
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
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

        private void Bind(Button button, UIManager.UIId id)
        {
            if (button == null || _ui == null)
                return;
            button.onClick.AddListener(() => _ui.Toggle(id));
        }

        private void OnCharacterPressed() => ToggleByKey(UIManager.UIId.Character);

        private void OnInventoryPressed() => ToggleByKey(UIManager.UIId.Inventory);

        private void OnSavePressed() => ToggleByKey(UIManager.UIId.Save);

        private void OnMapPressed() => ToggleByKey(UIManager.UIId.Map);

        private void ToggleByKey(UIManager.UIId id)
        {
            if (_shown && _ui != null)
                _ui.Toggle(id);
        }

        private void OnCancelPressed()
        {
            if (_shown && _ui != null && _ui.IsAnyPanelOpen)
                _ui.CloseAll();
        }

        private void OnModeChanged() => Apply();

        /// <summary>
        /// Field のときだけ表示。会話・戦闘中は非表示にしてセーブ等を開けなくする。
        /// GameObject ごと SetActive すると自分が止まって購読を失うため、
        /// CanvasGroup で隠すだけにして自身は生かし続ける。
        /// </summary>
        private void Apply()
        {
            // 探索中はパネルを開いたままでも切り替えられるように出しておく(メニューは探索中にしか開かない)。
            bool show = GameModeManager.Current == GameMode.Field;
            _shown = show;
            if (_group != null)
                _group.SetShown(show);
        }
    }
}
