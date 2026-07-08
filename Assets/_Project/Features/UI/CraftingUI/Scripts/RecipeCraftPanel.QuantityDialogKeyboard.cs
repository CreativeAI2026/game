using UnityEngine.InputSystem;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void UpdateQuantityDialogKeyboardControls()
        {
            if (_quantityDialog == null || !_quantityDialog.activeInHierarchy || _isCrafting)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                Decrease();
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                Increase();
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SetMaximum();
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SetMinimum();
        }
    }
}
