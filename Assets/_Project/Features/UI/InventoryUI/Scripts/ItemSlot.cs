using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    [RequireComponent(typeof(HoverScaleOnPointer))]
    public class ItemSlot : MonoBehaviour, IPointerClickHandler
    {
        private Image _iconImage;
        private HoverScaleOnPointer _hoverScale;
        private ItemStack _itemStack;
        private Inventory _controller;
        private Text _countText;

        private static readonly Color EquippedColor = new Color(0.95f, 0.8f, 0.4f, 0.5f);
        private static readonly Color NormalColor = Color.white;

        private void Awake()
        {
            _iconImage = GetOrCreateIconImage();
            _countText = GetComponentInChildren<Text>(true);
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            if (_hoverScale == null)
                _hoverScale = GetComponentInChildren<HoverScaleOnPointer>(true);

            if (_hoverScale != null && _iconImage != null)
            {
                _hoverScale.SetTarget(_iconImage.rectTransform);
                if (_countText != null)
                    _hoverScale.SetBounceTarget(_countText.rectTransform);
            }
            // cache controller reference if present in parents
            _controller = GetComponentInParent<Inventory>();
        }

        private Image GetOrCreateIconImage()
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image icon))
                return icon;

            var rootImage = GetComponent<Image>();
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconRect.SetAsFirstSibling();

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            if (rootImage != null)
            {
                iconImage.sprite = rootImage.sprite;
                iconImage.color = rootImage.color;
                rootImage.sprite = null;
                rootImage.color = Color.clear;
            }

            return iconImage;
        }

        private void OnEnable()
        {
            BindHoverTarget();
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;

            if (_iconImage == null)
                return;

            if (stack?.Data != null && stack.Data.icon != null)
            {
                _iconImage.sprite = stack.Data.icon;
                _iconImage.color = Color.white;
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.color = Color.clear;
            }

            // 数量表示（1個のときは非表示）
            if (_countText != null)
            {
                _countText.gameObject.SetActive(stack != null && stack.Count > 1);
                _countText.text = stack?.Count.ToString() ?? "";
            }

            SetEquipped(stack?.IsEquipped ?? false);

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
            if (_countText != null)
                _hoverScale.SetBounceTarget(_countText.rectTransform);
        }

        public ItemStack Stack => _itemStack;
        public ItemData Item => _itemStack?.Data;

        public void Select()
        {
            _hoverScale?.AcquireLock();
        }

        public void Deselect()
        {
            if (_hoverScale != null && _hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_controller != null)
            {
                _controller.SelectSlotByClick(this);
                return;
            }

            Select();
        }

        public void SetEquipped(bool isEquipped)
        {
            if (_iconImage != null)
                _iconImage.color = isEquipped ? EquippedColor : NormalColor;
        }
    }
}
