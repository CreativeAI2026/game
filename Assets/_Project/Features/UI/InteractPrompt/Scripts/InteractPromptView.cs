using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.Interaction;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.InteractPrompt
{
    /// <summary>
    /// 「[E] 扉を開ける」のような操作プロンプト(常駐)。<see cref="InteractPromptService"/> を購読してラベルを1つだけ表示する。
    /// UIRoot Prefab の子として同梱され、常駐は <see cref="UIRoot"/> が担う。
    /// パネル表示中と会話中は隠す(購読を切らないよう GameObject でなく Canvas.enabled で。<see cref="HudIconBar"/> と同じ)。
    /// </summary>
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _label;

        private Canvas _canvas;
        private UiRouter _router;
        private string _current;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _router = GetComponentInParent<UiRouter>(true);
        }

        private void OnEnable()
        {
            _current = InteractPromptService.Label;
            InteractPromptService.LabelChanged += OnLabelChanged;
            Apply();
        }

        private void OnDisable() => InteractPromptService.LabelChanged -= OnLabelChanged;

        private void OnLabelChanged(string label)
        {
            _current = label;
            Apply();
        }

        // パネルや会話は毎フレーム変わりうるので、表示可否だけは Update で見る(ラベルは購読)。
        private void Update() => Apply();

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

            if (_canvas != null)
                _canvas.enabled = !blocked;
            if (!blocked && _label != null && _label.text != _current)
                _label.text = _current;
        }
    }
}
