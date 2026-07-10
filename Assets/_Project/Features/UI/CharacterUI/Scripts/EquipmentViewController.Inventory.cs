using System.Linq;
using CreativeAI.Gameplay;

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

        private void SyncInventorySelection(ItemData item)
        {
            var selectedStack = FindStack(item);
            _inventory?.SelectItem(selectedStack);
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

            int equippedSlotIndex = _slots.FindIndex(slot => slot.Item == stack.Data);
            if (stack.IsEquipped || equippedSlotIndex >= 0)
            {
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

            if (
                _slots.Any(slot => slot != CurrentSlot && slot.Item == _selectedInventoryStack.Data)
            )
                return;

            UnequipItem(CurrentSlot.Item);
            CurrentSlot.EquipAnimated(_selectedInventoryStack.Data);
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
                ClearSlotWithItem(_selectedInventoryStack.Data);
                RefreshDetailFromCurrentSlot();
                return;
            }

            var currentItem = CurrentSlot.Item;
            if (currentItem == null)
                return;

            var stack = FindStack(currentItem);
            UnequipStack(stack, false);
            CurrentSlot.ClearAnimated();
            RefreshDetailFromCurrentSlot();
        }

        private void UnequipItem(ItemData item)
        {
            var stack = FindStack(item);
            UnequipStack(stack, false);
        }

        private void UnequipStack(ItemStack stack, bool keepSelected)
        {
            InventoryManager.Instance?.SetEquipped(stack, false);
            _inventory?.UpdateItemEquippedState(stack, false, keepSelected);
        }

        private void ClearSlotWithItem(ItemData item)
        {
            var slot = _slots.FirstOrDefault(candidate => candidate.Item == item);
            slot?.ClearAnimated();
        }

        private ItemStack FindStack(ItemData item)
        {
            return InventoryManager.Instance?.GetAllItems().Find(stack => stack.Data == item);
        }
    }
}
