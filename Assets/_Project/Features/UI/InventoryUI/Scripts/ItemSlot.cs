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
        private bool _isEquipped;
        private bool _isCraftAssigned;

        private static readonly Color EquippedColor = new Color(0.95f, 0.8f, 0.4f, 0.5f);
        private static readonly Color CraftAssignedColor = new Color(1f, 0.78f, 0.15f, 1f);
        private static readonly Color NormalColor = Color.white;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponentInParent<Inventory>();
        }

        private void OnEnable()
        {
            BindHoverTargets();
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
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
    }
}
