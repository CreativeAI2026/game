using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 即時食材UIの1枠の見た目(アイコンと所持数)。中身は <see cref="ImmediateFoodUIController"/> が流し込む。
    /// </summary>
    public sealed class ImmediateFoodSlotView : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TextMeshProUGUI _countText;

        private void Awake()
        {
            UnityEngine.Assertions.Assert.IsNotNull(_icon, $"{name}: _icon が未割当です");
            UnityEngine.Assertions.Assert.IsNotNull(_countText, $"{name}: _countText が未割当です");
        }

        public void Show(ItemStack stack)
        {
            _icon.sprite = stack.Data.icon;
            _icon.enabled = stack.Data.icon != null;
            _countText.text = stack.Count.ToString();
        }

        public void Clear()
        {
            _icon.sprite = null;
            _icon.enabled = false;
            _countText.text = string.Empty;
        }
    }
}
