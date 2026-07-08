using UnityEngine.InputSystem;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private void UpdateMaterialSlotKeyboardNavigation()
        {
            if (
                !isActiveAndEnabled
                || _selectedSlot == null
                || _slots.Count <= 0
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectMaterialSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectMaterialSlotByOffset(1);
        }

        private void SelectMaterialSlotByOffset(int offset)
        {
            int currentIndex = _slots.IndexOf(_selectedSlot);
            if (currentIndex < 0)
                return;

            int nextIndex = (currentIndex + offset + _slots.Count) % _slots.Count;
            SelectSlot(_slots[nextIndex]);
        }
    }
}
