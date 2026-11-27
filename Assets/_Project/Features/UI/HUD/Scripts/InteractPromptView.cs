using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 「[E] 扉を開ける」のような操作プロンプト(常駐)。<see cref="InteractPromptService"/> を購読してラベルを1つだけ表示する。
    /// UIRoot Prefab の子として同梱され、常駐は <see cref="UIManager"/> が担う。
    /// パネル表示中と会話中は隠す(購読を切らないよう GameObject でなく CanvasGroup で。<see cref="HudIconBarView"/> と同じ)。
    /// </summary>
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        private CanvasGroup _group;
        private GameModeManager _gameMode;
        private string _current;

        private void Awake()
        {
            _group = ResidentUIVisibility.GetOrAddGroup(gameObject);
        }

        private void OnEnable()
        {
            _current = InteractPromptService.Label;
            InteractPromptService.LabelChanged += OnLabelChanged;
            // 常駐生成順ではマネージャが先に居るので Instance を取れる。
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
            Apply();
        }

        private void OnDisable()
        {
            InteractPromptService.LabelChanged -= OnLabelChanged;
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
        }

        private void OnLabelChanged(string label)
        {
            _current = label;
            Apply();
        }

        private void OnModeChanged() => Apply();

        private void Apply()
        {
            bool blocked = string.IsNullOrEmpty(_current) || !GameModeManager.PlayerCanAct;

            if (_group != null)
                _group.SetShown(!blocked);
            if (!blocked && _label != null && _label.text != _current)
                _label.text = _current;
        }
    }
}
