using UnityEngine.InputSystem;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void UpdateQuantityDialogKeyboardControls()
        {
            if (
                _quantityDialogController == null
                || !_quantityDialogController.IsOpen
                || _isCrafting
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                _quantityDialogController.Decrement();
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                _quantityDialogController.Increment();
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                _quantityDialogController.SetMax();
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                _quantityDialogController.SetMin();
        }
    }
}
