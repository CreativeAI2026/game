using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public sealed class UiActionButtonStyle : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        private void OnEnable() => Refresh();

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (_button != null && _label != null)
                _label.color = _button.interactable ? UiVisualTheme.ActionInk : UiVisualTheme.Muted;
        }
    }
}
