using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>ボタンが押せるかどうかに合わせて、ボタンの文字の色を切り替える。</summary>
    public sealed class UIActionButtonStyle : MonoBehaviour
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
                _label.color = _button.interactable ? UIVisualTheme.ActionInk : UIVisualTheme.Muted;
        }
    }
}
