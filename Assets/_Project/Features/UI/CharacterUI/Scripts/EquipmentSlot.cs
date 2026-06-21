using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
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
        private Text _countText;
        private HoverScaleOnPointer _hoverScale;

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
            _countText = transform.Find("CountText")?.GetComponent<Text>();
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            if (_hoverScale == null)
                _hoverScale = GetComponentInChildren<HoverScaleOnPointer>(true);

            if (_hoverScale != null)
            {
                _hoverScale.SetTarget(_icon.rectTransform);
                if (_countText != null)
                    _hoverScale.SetBounceTarget(_countText.rectTransform);
                _hoverScale.SetGroup("equipment-slots");
                _hoverScale.SetReleaseLockOnOutsideClick(false);
            }

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
                UpdateCount();
            }
            else
            {
                _icon.sprite = null;
                _icon.gameObject.SetActive(false);
                _emptyText.gameObject.SetActive(true);
                if (_countText != null)
                    _countText.gameObject.SetActive(false);
            }
        }

        public void UpdateCount()
        {
            if (_countText == null || _item == null)
                return;

            if (_item == null)
            {
                _countText.gameObject.SetActive(false);
                return;
            }

            var stack = InventoryManager.Instance?.GetAllItems().Find(s => s.Data == _item);
            int count = stack?.Count ?? 1;

            _countText.gameObject.SetActive(count > 1);
            _countText.text = count.ToString();
        }

        public void SetFrameColor(Color color)
        {
            if (_frame != null)
                _frame.color = color;
        }

        public void SetSelected(bool selected)
        {
            if (_hoverScale == null)
                return;

            if (selected)
                _hoverScale.AcquireLock();
            else if (_hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }
    }
}
