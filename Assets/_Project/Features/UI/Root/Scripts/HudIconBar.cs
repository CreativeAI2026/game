using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面右上の円形ナビ(キャラ / インベ / セーブ)。HP の HUD とは別 Canvas に分ける
    /// (HP は入力を受けず頻繁に更新されるため)。
    /// ボタン押下で <see cref="UiRouter.Toggle"/> を呼び、<see cref="GameModeManager.OnModeChanged"/> を
    /// 購読して自分の Canvas を Field=表示 / Battle=非表示 に切り替える(モード連動は自分で行い、
    /// 外部の切替役は不要)。常駐 <see cref="UIRoot"/> 配下なので購読・配線は生成時の1回だけで済む。
    /// </summary>
    public sealed class HudIconBar : MonoBehaviour
    {
        [SerializeField]
        private UiRouter _router;

        [SerializeField]
        private Button _characterButton;

        [SerializeField]
        private Button _inventoryButton;

        [SerializeField]
        private Button _saveButton;

        [Header("モード連動で出し入れする自分の Canvas")]
        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private GraphicRaycaster _raycaster;

        private GameModeManager _gameMode;

        private void Awake()
        {
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();
            if (_raycaster == null)
                _raycaster = GetComponent<GraphicRaycaster>();

            Bind(_characterButton, UiRouter.UiId.Character);
            Bind(_inventoryButton, UiRouter.UiId.Inventory);
            Bind(_saveButton, UiRouter.UiId.Save);
        }

        private void OnEnable()
        {
            // 常駐生成順ではマネージャが先に居るので Instance を取れる。無ければ Field 既定で表示。
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.OnModeChanged += OnModeChanged;
            // 会話イベント中も隠す(セーブ/インベを開けなくする)。
            EventPlaybackService.PlayingChanged += OnPlaybackChanged;
            ApplyCurrent();
        }

        private void OnDisable()
        {
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            EventPlaybackService.PlayingChanged -= OnPlaybackChanged;
        }

        private void Bind(Button button, UiRouter.UiId id)
        {
            if (button == null || _router == null)
                return;
            button.onClick.AddListener(() => _router.Toggle(id));
        }

        private void OnModeChanged(GameMode mode) => ApplyCurrent();

        private void OnPlaybackChanged(bool playing) => ApplyCurrent();

        private void ApplyCurrent()
        {
            var mode = _gameMode != null ? _gameMode.CurrentMode : GameMode.Field;
            Apply(mode);
        }

        /// <summary>
        /// Field かつ会話イベント中でないときだけ表示。Battle・会話中は非表示にしてセーブ等を開けなくする
        /// (documents/Specification.md §2.2, §5)。GameObject ごと SetActive すると自分が止まって購読を失うため、
        /// Canvas / Raycaster を無効化するだけにして自身は生かし続ける。
        /// </summary>
        private void Apply(GameMode mode)
        {
            bool show = mode == GameMode.Field && !EventPlaybackService.IsPlaying;
            if (_canvas != null)
                _canvas.enabled = show;
            if (_raycaster != null)
                _raycaster.enabled = show;
        }
    }
}
