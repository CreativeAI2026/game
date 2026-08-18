using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class InventoryView
    {
        private void Update()
        {
            if (
                !isActiveAndEnabled
                || !_interactionEnabled
                || _currentSelectedSlot == null
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectSlotByOffset(1);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SelectSlotVertically(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SelectSlotVertically(1);
            else if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
                SubmitSelectedSlot();
        }

        public void SubmitSelectedSlot()
        {
            if (
                !_interactionEnabled
                || _currentSelectedSlot == null
                || _selectedStack == null
                || _selectedStack.Count <= 0
            )
                return;

            OnSlotSubmitted?.Invoke(_selectedStack);
        }

        private void SelectSlotByOffset(int offset)
        {
            if (!TryGetCurrentSlotIndex(out int currentIndex, out int slotCount))
                return;

            if (slotCount <= 1)
                return;

            int nextIndex = (currentIndex + offset + slotCount) % slotCount;
            SelectSlotAt(nextIndex);
        }

        private void SelectSlotVertically(int rowOffset)
        {
            if (!TryGetCurrentSlotIndex(out int currentIndex, out int slotCount))
                return;

            int columns = GetColumnCount();
            if (slotCount <= columns)
                return;

            int nextIndex = currentIndex + columns * rowOffset;

            if (nextIndex < 0)
                nextIndex = GetBottomIndexInColumn(currentIndex % columns, columns, slotCount);
            else if (nextIndex >= slotCount)
                nextIndex = currentIndex % columns;

            if (nextIndex == currentIndex)
                return;

            SelectSlotAt(nextIndex);
        }

        private bool TryGetCurrentSlotIndex(out int currentIndex, out int slotCount)
        {
            slotCount = _visibleSlots.Count;
            currentIndex = _visibleSlots.IndexOf(_currentSelectedSlot);
            if (
                _currentSelectedSlot == null
                || currentIndex < 0
                || slotCount <= 0
                || !_currentSelectedSlot.gameObject.activeSelf
            )
                return false;

            return true;
        }

        private int GetColumnCount()
        {
            if (
                _slotsRoot != null
                && _slotsRoot.TryGetComponent(out GridLayoutGroup grid)
                && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            )
            {
                return Mathf.Max(1, grid.constraintCount);
            }

            return Mathf.Max(1, _visibleSlots.Count);
        }

        private int GetBottomIndexInColumn(int column, int columns, int slotCount)
        {
            int bottomIndex = column;
            while (bottomIndex + columns < slotCount)
                bottomIndex += columns;

            if (bottomIndex != column || slotCount <= columns)
                return bottomIndex;

            return slotCount - 1;
        }

        private void SelectSlotAt(int index)
        {
            if (index < 0 || index >= _visibleSlots.Count)
                return;

            var slot = _visibleSlots[index];
            if (slot != null && slot.gameObject.activeInHierarchy)
                SelectSlot(slot);
        }
    }
}
