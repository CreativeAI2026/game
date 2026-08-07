using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController
    {
        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotClicked -= OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotClicked += OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotClicked -= OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
        }

        private void BindInventoryItemsRequested()
        {
            if (_inventory == null)
                return;

            _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnInventoryItemsRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
        }

        private void UnbindInventoryItemsRequested()
        {
            if (_inventory != null)
            {
                _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
                _inventory.ItemsRequested -= OnInventoryItemsRequested;
            }
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition _definition,
            int _tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            _inventory?.RequestItems(_inventoryCategory, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null || category != _inventoryCategory)
                return;

            var items = InventoryManager.Instance?.GetItemsByCategory(_inventoryCategory);
            _inventory.SetItems(items, scrollMode);
        }

        private void BindInventoryChangedEvent()
        {
            if (_subscribedToInventoryChanged || InventoryManager.Instance == null)
                return;

            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
            _subscribedToInventoryChanged = true;
        }

        private void UnbindInventoryChangedEvent()
        {
            if (!_subscribedToInventoryChanged)
                return;

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;

            _subscribedToInventoryChanged = false;
        }

        private void OnInventoryChanged()
        {
            _inventory?.RefreshCurrentTab();
            SyncEquipmentSlotsWithInventory();
        }

        private void SyncInventorySelection(ItemStack stack)
        {
            _inventory?.SelectItem(stack);
        }

        private void OnInventorySlotSelected(ItemStack stack)
        {
            if (!IsValidStack(stack))
                return;

            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack.Data);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (!IsValidStack(stack) || !HasSlots)
                return;

            int equippedSlotIndex = _slots.FindIndex(slot => slot.Stack == stack);
            if (stack.IsEquipped || equippedSlotIndex >= 0)
            {
                if (equippedSlotIndex >= 0)
                    SelectAndRotateSlot(equippedSlotIndex);

                _selectedInventoryStack = stack;
                UnequipCurrentSlot();
                return;
            }

            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack.Data);
            EquipSelectedItem();
        }

        private bool IsValidStack(ItemStack stack)
        {
            return stack?.Data != null && stack.Data.category == _inventoryCategory;
        }

        private void EquipSelectedItem()
        {
            if (_selectedInventoryStack == null || CurrentSlot == null)
                return;

            if (_slots.Any(slot => slot != CurrentSlot && slot.Stack == _selectedInventoryStack))
                return;

            UnequipStack(CurrentSlot.Stack, false);
            CurrentSlot.EquipAnimated(_selectedInventoryStack);
            InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, true);

            _inventory?.UpdateItemEquippedState(_selectedInventoryStack, true, true);
            _detailPanel?.Show(_selectedInventoryStack.Data);

            SelectNextEmptySlot();
            RefreshDetailFromCurrentSlot();
        }

        private void UnequipCurrentSlot()
        {
            if (CurrentSlot == null)
                return;

            if (_selectedInventoryStack != null && _selectedInventoryStack.IsEquipped)
            {
                UnequipStack(_selectedInventoryStack, true);
                ClearSlotWithStack(_selectedInventoryStack);
                RefreshDetailFromCurrentSlot();
                return;
            }

            var currentStack = CurrentSlot.Stack;
            if (currentStack == null)
                return;

            UnequipStack(currentStack, false);
            CurrentSlot.ClearAnimated();
            RefreshDetailFromCurrentSlot();
        }

        private void UnequipStack(ItemStack stack, bool keepSelected)
        {
            InventoryManager.Instance?.SetEquipped(stack, false);
            _inventory?.UpdateItemEquippedState(stack, false, keepSelected);
        }

        private void ClearSlotWithStack(ItemStack stack)
        {
            var slot = _slots.FirstOrDefault(candidate => candidate.Stack == stack);
            slot?.ClearAnimated();
        }

        private void SyncEquipmentSlotsWithInventory()
        {
            if (!HasSlots || InventoryManager.Instance == null)
                return;

            bool currentSlotChanged = false;
            bool selectedStackRemoved = false;

            foreach (var slot in _slots)
            {
                var stack = slot?.Stack;
                if (stack == null)
                    continue;

                if (InventoryManager.Instance.InventoryService.ContainsStack(stack))
                {
                    slot.UpdateCount();
                    continue;
                }

                if (_selectedInventoryStack == stack)
                    selectedStackRemoved = true;
                if (slot == CurrentSlot)
                    currentSlotChanged = true;

                InventoryManager.Instance.SetEquipped(stack, false);
                slot.Clear();
            }

            if (selectedStackRemoved)
                _selectedInventoryStack = null;

            if (currentSlotChanged)
            {
                SyncInventorySelection(CurrentSlot?.Stack);
                RefreshDetailFromCurrentSlot();
            }
        }
    }
}
