using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void ResolveQuantityDialogReferences()
        {
            _quantityDialogPanel ??= FindGameObjectIn(transform, "CQD-Panel");
            _quantityDialog ??= FindQuantityDialog();
            _quantityDialogController ??=
                _quantityDialog != null
                    ? _quantityDialog.GetComponent<CraftQuantityDialog>()
                    : GetComponentInChildren<CraftQuantityDialog>(true);

            if (_quantityDialogPanel == null)
                WarnMissingReferenceOnce(ref _warnedMissingQuantityDialogPanel, "CQD-Panel");
            if (_quantityDialog == null)
                WarnMissingReferenceOnce(ref _warnedMissingQuantityDialog, "CraftQuantityDialog");
            if (_quantityDialogController == null)
                WarnMissingReferenceOnce(
                    ref _warnedMissingQuantityDialogController,
                    nameof(CraftQuantityDialog)
                );
        }

        private void BindDialog()
        {
            ResolveQuantityDialogReferences();
        }

        private void OpenQuantityDialog()
        {
            ResolveAllReferences();
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
                    nameof(CraftQuantityDialog)
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

        private GameObject FindQuantityDialog()
        {
            var dialogTransform =
                _quantityDialogPanel != null
                    ? FindIn(_quantityDialogPanel.transform, "CraftQuantityDialog")
                    : Find("CraftQuantityDialog");

            return dialogTransform != null ? dialogTransform.gameObject : null;
        }
    }
}
