using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory
    {
        private void Update()
        {
            if (
                !isActiveAndEnabled
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
            currentIndex = -1;
            slotCount = _slotsRoot != null ? _slotsRoot.childCount : 0;
            if (_slotsRoot == null || _currentSelectedSlot == null || slotCount <= 0)
                return false;

            for (int i = 0; i < slotCount; i++)
            {
                if (_slotsRoot.GetChild(i).GetComponent<ItemSlot>() != _currentSelectedSlot)
                    continue;

                currentIndex = i;
                return true;
            }

            return false;
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

            return Mathf.Max(1, _slotsRoot != null ? _slotsRoot.childCount : 1);
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
            if (_slotsRoot == null || index < 0 || index >= _slotsRoot.childCount)
                return;

            var slot = _slotsRoot.GetChild(index).GetComponent<ItemSlot>();
            if (slot != null && slot.gameObject.activeInHierarchy)
                SelectSlot(slot);
        }
    }
}
