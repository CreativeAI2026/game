using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.Interaction;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.InteractPrompt
{
    /// <summary>
    /// 「[E] 扉を開ける」のような操作プロンプト(常駐)。
    /// <see cref="InteractPromptService"/> を購読して、ワールド側が出したラベルを1つだけ表示する。
    ///
    /// 仕様§6のとおり <see cref="UIRoot"/> が束ねる UI レイヤーの一部で、UIRoot Prefab の子として
    /// 同梱される(常駐・単一化・DontDestroyOnLoad は UIRoot が担う)。
    /// 開くパネル(インベ/キャラ/セーブ/調合)表示中と会話中は隠す。隠すのは
    /// <see cref="HudIconBar"/> / QuickFoodBar と同じく <b>Canvas.enabled</b> で、
    /// GameObject は殺さない(購読が切れないようにするため)。
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
