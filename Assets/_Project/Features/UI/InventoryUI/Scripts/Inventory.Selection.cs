using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory
    {
        public void SelectSlot(ItemSlot slot)
        {
            if (slot == null)
                return;

            if (_currentSelectedSlot != null && _currentSelectedSlot != slot)
                _currentSelectedSlot.Deselect();

            slot.Select();
            _currentSelectedSlot = slot;
            _selectedStack = slot.Stack;
            _detailPanel?.Show(slot.Item);

            DisableNavigationOnce();
        }

        public void SelectSlotByClick(ItemSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
            OnSlotClicked?.Invoke(slot.Stack);
        }

        public void SelectSlotByDoubleClick(ItemSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
            OnSlotDoubleClicked?.Invoke(slot.Stack);
        }

        public void HighlightEquippedItem(ItemStack stack)
        {
            if (_equippedSlot != null)
                _equippedSlot.SetEquipped(false);

            _equippedSlot = _currentSelectedSlot;
            if (_equippedSlot != null)
                _equippedSlot.SetEquipped(true);
        }

        public void UpdateItemEquippedState(ItemStack stack, bool isEquipped, bool keepSelected)
        {
            if (stack == null || _slotsRoot == null)
                return;

            var slot = FindVisibleSlot(stack);
            if (slot == null)
                return;

            slot.SetEquipped(isEquipped);
            if (isEquipped)
                _equippedSlot = slot;
            else if (_equippedSlot == slot)
                _equippedSlot = null;

            if (!keepSelected)
                return;

            _selectedStack = stack;
            _currentSelectedSlot = slot;
            slot.Select();
        }

        public void SelectItem(ItemStack stack)
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _selectedStack = stack;
            _currentSelectedSlot = FindVisibleSlot(stack);
            _currentSelectedSlot?.Select();
        }

        public void ClearSelection()
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _currentSelectedSlot = null;
            _selectedStack = null;
            CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        public void ResetViewState()
        {
            ClearSelection();
            _equippedSlot = null;
            _detailPanel?.Clear();

            if (_useFixedCategory)
                RefreshCurrentTab();
            else
                _tabGroup?.ResetToFirstTab();

            if (!_useFixedCategory && _tabGroup == null)
                RefreshCurrentTab();
        }

        public void SetCraftAssignedItems(IEnumerable<ItemData> items)
        {
            _craftAssignedItems.Clear();
            if (items != null)
            {
                foreach (var item in items)
                    if (item != null)
                        _craftAssignedItems.Add(item);
            }

            if (_slotsRoot == null)
                return;

            foreach (var slot in _slotsRoot.GetComponentsInChildren<ItemSlot>(true))
                slot.SetCraftAssigned(slot.Item != null && _craftAssignedItems.Contains(slot.Item));
        }

        public void ResetToTop()
        {
            if (_slotsRoot is RectTransform contentRect)
            {
                var scroll = contentRect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1f;
            }

            if (_slotsRoot != null && _slotsRoot.childCount > 0)
                SelectSlot(_slotsRoot.GetChild(0).GetComponent<ItemSlot>());
        }

        private void DisableNavigationOnce()
        {
            if (_navigationDisabled || EventSystem.current == null)
                return;

            _previousSendNavigationEvents = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            _navigationDisabled = true;
        }

        private void RestoreNavigation()
        {
            if (!_navigationDisabled || EventSystem.current == null)
                return;

            EventSystem.current.sendNavigationEvents = _previousSendNavigationEvents;
            _navigationDisabled = false;
        }
    }
}
