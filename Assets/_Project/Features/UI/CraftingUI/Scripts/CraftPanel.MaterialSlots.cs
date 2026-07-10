using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private void InitializeSlots()
        {
            if (_slotsRoot == null)
                return;

            UnbindMaterialSlots();
            _slots.Clear();
            _selectedSlot = null;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slotObject = _slotsRoot.GetChild(i).gameObject;
                var slot = slotObject.GetComponent<MaterialSlot>();
                if (slot == null && slotObject.name.StartsWith("MaterialSlot"))
                    slot = slotObject.AddComponent<MaterialSlot>();
                if (slot == null)
                    continue;

                slot.NormalizeVisualState();
                slot.Clicked += OnMaterialSlotClicked;
                slot.DoubleClicked += OnMaterialSlotDoubleClicked;
                _slots.Add(slot);
            }
        }

        private void UnbindMaterialSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnMaterialSlotClicked;
                slot.DoubleClicked -= OnMaterialSlotDoubleClicked;
            }
        }

        private void Subscribe()
        {
            if (_inventory == null || _isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (_inventory != null && _isSubscribed)
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;

            _isSubscribed = false;
        }

        private void SelectFirstSlotIfNeeded()
        {
            if (_selectedSlot == null && _slots.Count > 0)
                SelectSlot(_slots[0]);

            ClaimMaterialSlotFocus();
        }

        private IEnumerator EnsureInitialSelectionNextFrame()
        {
            yield return null;

            if (_slots.Count > 0)
                SelectSlot(_slots[0]);

            ClaimMaterialSlotFocus();

            _initialSelectionRoutine = null;
        }

        private void ResetSlots()
        {
            _selectedSlot = null;

            foreach (var slot in _slots)
            {
                slot.Clear();
                slot.SetSelected(false);
            }

            SyncInventoryAssignedColors();
            _inventory?.ResetViewState();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private void SelectSlot(MaterialSlot selectedSlot)
        {
            if (selectedSlot == null)
                return;

            var previousSlot = _selectedSlot;
            _selectedSlot = selectedSlot;
            foreach (var slot in _slots)
                slot.SetSelected(slot == selectedSlot);

            var selectedStack = InventoryManager
                .Instance?.GetAllItems()
                .Find(stack => stack.Data == selectedSlot.Item);
            if (selectedStack != null)
                _inventory?.SelectItem(selectedStack);
            else
                _inventory?.ClearSelection();

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != selectedSlot
                && previousSlot.Item == null
                && selectedSlot.Item == null;
            _detailPanel?.Show(selectedSlot.Item, EmptyMaterialLabel, changedBetweenEmptySlots);
        }

        private void OnMaterialSlotClicked(MaterialSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
        }

        private void OnMaterialSlotDoubleClicked(MaterialSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            ClearSlot(slot);
        }

        private void ClaimMaterialSlotFocus()
        {
            if (_selectedSlot != null)
                CreativeAI.UI.SlotKeyboardFocus.Claim(this);
        }

        private void ClearSlot(MaterialSlot slot)
        {
            if (slot == null)
                return;

            SelectSlot(slot);

            slot.ClearMaterialAnimated(() =>
            {
                SyncInventoryAssignedColors();
                RefreshDetailFromSelectedCraftSlot();
                UpdateCraftButton();
            });

            _inventory?.ClearSelection();
            _detailPanel?.Clear();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (_selectedSlot == null || stack?.Data == null || stack.Count <= 0)
                return;

            if (ClearAssignedMaterialSlot(stack.Data))
                return;

            int currentCount = _selectedSlot.Item == stack.Data ? _selectedSlot.Count : 0;
            int desiredCount = Mathf.Min(currentCount + 1, stack.Count);

            ClearMaterialFromOtherSlots(stack.Data, _selectedSlot);

            _selectedSlot.SetMaterialAnimated(stack.Data, desiredCount);
            SyncInventoryAssignedColors();
            _detailPanel?.Show(stack.Data);
            SelectNextEmptyCraftSlot();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private bool ClearAssignedMaterialSlot(ItemData item)
        {
            var assignedSlot = _slots.FirstOrDefault(slot => slot.Item == item);
            if (assignedSlot == null)
                return false;

            SelectSlot(assignedSlot);

            assignedSlot.ClearMaterialAnimated(() =>
            {
                SyncInventoryAssignedColors();
                _inventory?.ClearSelection();
                RefreshDetailFromSelectedCraftSlot();
                UpdateCraftButton();
            });

            _inventory?.ClearSelection();
            _detailPanel?.Clear();

            return true;
        }

        private void SelectNextEmptyCraftSlot()
        {
            if (_selectedSlot == null || _slots.Count <= 1)
                return;

            int selectedIndex = _slots.IndexOf(_selectedSlot);
            if (selectedIndex < 0)
                return;

            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (selectedIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Item != null)
                    continue;

                SelectSlot(_slots[slotIndex]);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedItems(
                _slots.Where(slot => slot.Item != null).Select(slot => slot.Item)
            );
        }

        private void RefreshDetailFromSelectedCraftSlot()
        {
            if (_detailPanel == null || _selectedSlot == null)
                return;

            _detailPanel.Show(_selectedSlot.Item, EmptyMaterialLabel);
        }

        private void ClearMaterialFromOtherSlots(ItemData item, MaterialSlot destinationSlot)
        {
            foreach (var slot in _slots)
            {
                if (slot == destinationSlot || slot.Item != item)
                    continue;

                slot.ClearMaterialAnimated();
            }
        }
    }
}
