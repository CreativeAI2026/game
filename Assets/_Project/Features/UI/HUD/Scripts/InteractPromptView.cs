using CreativeAI.Core;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 「[E] 扉を開ける」のような操作プロンプト(常駐)。<see cref="InteractPromptService"/> を購読してラベルを1つだけ表示する。
    /// UIRoot Prefab の子として同梱され、常駐は <see cref="UIRoot"/> が担う。
    /// パネル表示中と会話中は隠す(購読を切らないよう GameObject でなく CanvasGroup で。<see cref="HudIconBarView"/> と同じ)。
    /// </summary>
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        private CanvasGroup _group;
        private UiRouter _router;
        private GameModeManager _gameMode;
        private string _current;

        private void Awake()
        {
            _group = ResidentUiVisibility.GetOrAddGroup(gameObject);
            _router = GetComponentInParent<UiRouter>(true);
        }

        private void OnEnable()
        {
            _current = InteractPromptService.Label;
            InteractPromptService.LabelChanged += OnLabelChanged;
            // 常駐生成順ではマネージャが先に居るので Instance を取れる。
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.OnModeChanged += OnModeChanged;
            EventPlaybackService.PlayingChanged += OnPlaybackChanged;
            UiPanelService.OpenChanged += OnPanelOpenChanged;
            Apply();
        }

        private void OnDisable()
        {
            InteractPromptService.LabelChanged -= OnLabelChanged;
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            EventPlaybackService.PlayingChanged -= OnPlaybackChanged;
            UiPanelService.OpenChanged -= OnPanelOpenChanged;
        }

        private void OnLabelChanged(string label)
        {
            _current = label;
            Apply();
        }

        private void OnModeChanged(GameMode mode) => Apply();

        private void OnPlaybackChanged(bool playing) => Apply();

        private void OnPanelOpenChanged(bool open) => Apply();

        private void Apply()
        {
            bool inBattle =
                GameModeManager.Instance != null
                && GameModeManager.Instance.CurrentMode == GameMode.Battle;
            bool blocked =
                string.IsNullOrEmpty(_current)
                || inBattle
                || EventPlaybackService.IsPlaying
                || (_router != null && _router.IsAnyPanelOpen);

            if (_group != null)
                _group.SetShown(!blocked);
            if (!blocked && _label != null && _label.text != _current)
                _label.text = _current;
        }
    }
}
