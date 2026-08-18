using UnityEngine.InputSystem;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController
    {
        private void Update()
        {
            if (
                !isActiveAndEnabled
                || !HasSlots
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectEquipmentSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectEquipmentSlotByOffset(1);
        }

        private void SelectEquipmentSlotByOffset(int offset)
        {
            if (IsSlotInputLocked())
                return;

            int nextIndex = (_currentSlotIndex + offset + _slots.Count) % _slots.Count;
            SelectAndRotateSlot(nextIndex);
        }
    }
}
