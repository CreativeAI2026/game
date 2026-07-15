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
            var slot = FindVisibleSlot(stack);
            if (slot != null)
                slot.SetEquipped(stack.IsEquipped);
        }

        public void UpdateItemEquippedState(ItemStack stack, bool isEquipped, bool keepSelected)
        {
            if (stack == null || _slotsRoot == null)
                return;

            var slot = FindVisibleSlot(stack);
            if (slot == null)
                return;

            slot.SetEquipped(isEquipped);

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
            _craftAssignedStacks.Clear();
            if (items != null)
            {
                foreach (var item in items)
                    if (item != null)
                        _craftAssignedItems.Add(item);
            }

            RefreshCraftAssignedSlots();
        }

        public void SetCraftAssignedStacks(IEnumerable<ItemStack> stacks)
        {
            _craftAssignedItems.Clear();
            _craftAssignedStacks.Clear();
            if (stacks != null)
            {
                foreach (var stack in stacks)
                    if (stack != null)
                        _craftAssignedStacks.Add(stack);
            }

            RefreshCraftAssignedSlots();
        }

        private bool IsCraftAssigned(ItemStack stack) =>
            stack != null
            && (
                _craftAssignedStacks.Contains(stack)
                || (stack.Data != null && _craftAssignedItems.Contains(stack.Data))
            );

        private void RefreshCraftAssignedSlots()
        {
            if (_slotsRoot == null)
                return;

            foreach (var slot in _slotsRoot.GetComponentsInChildren<ItemSlot>(true))
                slot.SetCraftAssigned(IsCraftAssigned(slot.Stack));
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
