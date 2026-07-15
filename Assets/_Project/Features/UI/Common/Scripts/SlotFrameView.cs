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

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_frame != null)
                return;

            var frameTransform =
                transform.Find("VisualRoot/Frame")
                ?? transform.Find("Frame")
                ?? transform.Find("VisualRoot/SelectedFrame")
                ?? transform.Find("SelectedFrame");
            _frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
        }
#endif
    }
}
