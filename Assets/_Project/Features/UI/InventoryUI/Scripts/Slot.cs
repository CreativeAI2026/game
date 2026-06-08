using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class Slot : MonoBehaviour
    {
        private Image _iconImage;
        private HoverScaleOnPointer _hoverScale;
        private ItemData _itemData;

        private void Awake()
        {
            _iconImage = GetComponentInChildren<Image>(true);
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            if (_hoverScale == null)
                _hoverScale = GetComponentInChildren<HoverScaleOnPointer>(true);

            if (_hoverScale != null && _iconImage != null)
                _hoverScale.SetTarget(_iconImage.rectTransform);
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
    }
}
