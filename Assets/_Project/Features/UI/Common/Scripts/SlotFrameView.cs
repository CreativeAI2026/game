using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class SlotFrameView : MonoBehaviour
    {
        [SerializeField]
        private Image _frame;

        [SerializeField]
        private Color _selectedColor = new(1f, 0.78f, 0.15f, 0.9f);

        [SerializeField]
        private Color _normalColor = new(1f, 1f, 1f, 0.2f);

        private bool _hasWarnedMissingFrame;

        public Color GetColor(bool selected) => selected ? _selectedColor : _normalColor;

        public void SetSelected(bool selected) => SetColor(GetColor(selected));

        public void SetColor(Color color)
        {
            if (!ResolveFrame())
                return;

            _frame.color = color;
        }

        private bool ResolveFrame()
        {
            _frame ??= GetComponent<Image>();
            if (_frame != null)
                return true;

            if (!_hasWarnedMissingFrame)
            {
                _hasWarnedMissingFrame = true;
                Debug.LogWarning(
                    $"{nameof(SlotFrameView)} '{name}' にFrame Imageがないため、枠表示をスキップします。Prefab上で設定してください。",
                    this
                );
            }

            return false;
        }
    }
}
