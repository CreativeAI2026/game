using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private bool ValidateQuantityDialogReferences()
        {
            bool valid = true;
            if (_quantityDialogPanel == null)
            {
                WarnMissingReferenceOnce(
                    ref _warnedMissingQuantityDialogPanel,
                    nameof(_quantityDialogPanel)
                );
                valid = false;
            }
            if (_quantityDialog == null)
            {
                WarnMissingReferenceOnce(ref _warnedMissingQuantityDialog, nameof(_quantityDialog));
                valid = false;
            }
            if (_quantityDialogController == null)
            {
                WarnMissingReferenceOnce(
                    ref _warnedMissingQuantityDialogController,
                    nameof(_quantityDialogController)
                );
                valid = false;
            }

            return valid;
        }

        private void BindDialog()
        {
            ValidateQuantityDialogReferences();
        }

        private void OpenQuantityDialog()
        {
            if (!ValidateQuantityDialogReferences())
                return;

            int max = GetMaximumCraftable();
            if (_selectedRecipe == null)
                return;

            if (max <= 0)
            {
                PlayMissingMaterialsWarning();
                return;
            }

            if (_quantityDialogController == null)
            {
                WarnMissingReferenceOnce(
                    ref _warnedMissingQuantityDialogController,
                    nameof(_quantityDialogController)
                );
                return;
            }

            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, max));
            _quantityDialogController.Show(
                _selectedRecipe.resultItem?.icon,
                _selectedRecipe.resultItem?.itemName,
                1,
                max,
                _quantity,
                OnQuantityConfirmed
            );
        }

        private void CloseQuantityDialog()
        {
            _quantityDialogController?.Hide();
        }

        private void CloseQuantityDialogImmediately()
        {
            _quantityDialogController?.HideImmediate();
        }

        private void OnQuantityConfirmed(int quantity)
        {
            _quantity = quantity;
            StartCraft();
        }

        private int GetMaximumCraftable()
        {
            if (_selectedRecipe == null)
                return 0;

            return InventoryManager.Instance?.GetMaximumCraftable(_selectedRecipe) ?? 0;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Quantity Dialog References")]
        private void AutoAssignQuantityDialogReferences()
        {
            _quantityDialogPanel ??= UIChildFinder.FindGameObject(transform, "CQD-Panel");
            if (_quantityDialogPanel != null)
            {
                _quantityDialog ??= UIChildFinder.FindGameObject(
                    _quantityDialogPanel.transform,
                    "CraftQuantityDialog"
                );
            }

            _quantityDialogController ??= _quantityDialog?.GetComponent<CraftQuantityDialog>();
        }
#endif
    }
}
