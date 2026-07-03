using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class ItemSlot : BaseItemSlot, IPointerClickHandler
    {
        private ItemStack _itemStack;
        private Inventory _controller;
        private RectTransform _visualRootRect;
        private bool _isEquipped;
        private bool _isCraftAssigned;

        private static readonly Color EquippedColor = new Color(0.95f, 0.8f, 0.4f, 0.5f);
        private static readonly Color CraftAssignedColor = new Color(1f, 0.78f, 0.15f, 1f);
        private static readonly Color NormalColor = Color.white;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponentInParent<Inventory>();
            ConfigureVisualRootHover();
        }

        private void OnEnable()
        {
            ConfigureVisualRootHover();
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
            ConfigureVisualRootHover();
            SetEquipped(stack?.IsEquipped ?? false);
        }

        public ItemStack Stack => _itemStack;

        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            _hoverScale?.SetReleaseLockOnOutsideClick(release);
        }

        public override void Select()
        {
            base.Select();
        }

        public override void Deselect()
        {
            base.Deselect();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_controller != null)
            {
                if (
                    eventData.button == PointerEventData.InputButton.Left
                    && eventData.clickCount >= 2
                )
                {
                    _controller.SelectSlotByDoubleClick(this);
                    return;
                }

                _controller.SelectSlotByClick(this);
                return;
            }

            Select();
        }

        public void SetEquipped(bool isEquipped)
        {
            _isEquipped = isEquipped;
            RefreshColor();
        }

        public void SetCraftAssigned(bool isAssigned)
        {
            _isCraftAssigned = isAssigned;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (_iconImage == null)
                return;

            _iconImage.color =
                _isCraftAssigned ? CraftAssignedColor
                : _isEquipped ? EquippedColor
                : NormalColor;
        }

        private void ConfigureVisualRootHover()
        {
            ResolveVisualRoot();

            if (_hoverScale == null || _visualRootRect == null)
                return;

            _hoverScale.SetTarget(_visualRootRect);
            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        private void ResolveVisualRoot()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            if (_visualRootRect == null)
                _visualRootRect = CreateVisualRoot();

            if (_iconImage != null)
            {
                var iconRect = _iconImage.rectTransform;
                if (iconRect.parent != _visualRootRect)
                    iconRect.SetParent(_visualRootRect, false);

                iconRect.SetAsFirstSibling();
                StretchToFill(iconRect);
            }

            if (_countContainer != null)
            {
                if (_countContainer.parent != _visualRootRect)
                    _countContainer.SetParent(_visualRootRect, false);

                _countContainer.SetAsLastSibling();
                _countContainer.anchorMin = new Vector2(0f, 0f);
                _countContainer.anchorMax = new Vector2(1f, 0f);
                _countContainer.pivot = new Vector2(0.5f, 0f);
                _countContainer.anchoredPosition = Vector2.zero;
                _countContainer.sizeDelta = new Vector2(0f, 40f);
                _countContainer.localScale = Vector3.one;
            }
        }

        private RectTransform CreateVisualRoot()
        {
            var visualRootObject = new GameObject("VisualRoot", typeof(RectTransform));
            var rect = visualRootObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            StretchToFill(rect);
            return rect;
        }

        private static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }
    }
}
