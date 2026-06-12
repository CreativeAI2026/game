using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class ItemSlot : MonoBehaviour, IPointerClickHandler
    {
        private Image _iconImage;
        private HoverScaleOnPointer _hoverScale;
        private ItemData _itemData;
        private InventoryUIController _controller;

        private void Awake()
        {
            _iconImage = GetComponentInChildren<Image>(true);
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            if (_hoverScale == null)
                _hoverScale = GetComponentInChildren<HoverScaleOnPointer>(true);

            if (_hoverScale != null && _iconImage != null)
                _hoverScale.SetTarget(_iconImage.rectTransform);

            // cache controller reference if present in parents
            _controller = GetComponentInParent<InventoryUIController>();
        }

        private void OnEnable()
        {
            BindHoverTarget();
        }

        public void SetItem(ItemData item)
        {
            _itemData = item;

            if (_iconImage == null)
                return;

            if (item != null && item.icon != null)
            {
                _iconImage.sprite = item.icon;
                _iconImage.color = Color.white;
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.color = new Color(0, 0, 0, 0);
            }

            BindHoverTarget();
        }

        private void BindHoverTarget()
        {
            if (_hoverScale == null)
                return;

            if (_iconImage == null)
                _iconImage = GetComponentInChildren<Image>(true);

            if (_iconImage != null)
                _hoverScale.SetTarget(_iconImage.rectTransform);
        }

        public HoverScaleOnPointer Hover => _hoverScale;

        public ItemData Item => _itemData;

        public void Select()
        {
            _hoverScale?.AcquireLock();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_controller != null)
            {
                _controller.SelectSlot(this);
                return;
            }

            Select();
        }
    }
}
