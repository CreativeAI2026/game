using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button)), RequireComponent(typeof(Image))]
    public class EquipmentSlot : MonoBehaviour
    {
        public Button Button { get; private set; }
        private Image _icon;
        private Text _emptyText;
        private Image _frame;
        private ItemData _item;
        public ItemData Item
        {
            get => _item;
            set
            {
                _item = value;
                UpdateSlot();
            }
        }

        public void Init()
        {
            Button = GetComponent<Button>();
            _icon = transform.Find("Icon").GetComponent<Image>();
            _emptyText = transform.Find("EmptyText").GetComponent<Text>();
            _frame = GetComponent<Image>();
            UpdateSlot();
        }

        private void UpdateSlot()
        {
            if (_icon == null || _emptyText == null)
                return;

            if (_item != null && _item.icon != null)
            {
                _icon.sprite = _item.icon;
                _icon.gameObject.SetActive(true);
                _emptyText.gameObject.SetActive(false);
            }
            else
            {
                _icon.sprite = null;
                _icon.gameObject.SetActive(false);
                _emptyText.gameObject.SetActive(true);
            }
        }

        public void SetFrameColor(Color color)
        {
            if (_frame != null)
                _frame.color = color;
        }
    }
}
