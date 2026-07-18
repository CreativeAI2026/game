using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanelController
    {
        private bool ValidateQuantityDialogReferences()
        {
            if (_quantityDialogController == null)
            {
                WarnMissingReferenceOnce(
                    ref _warnedMissingQuantityDialogController,
                    nameof(_quantityDialogController)
                );
                return false;
            }

            return true;
        }

        private void BindDialog()
        {
            if (!ValidateQuantityDialogReferences())
                return;

            _quantityDialogController.QuantityChanged -= OnQuantityChanged;
            _quantityDialogController.QuantityChanged += OnQuantityChanged;
        }

        private void UnbindDialog()
        {
            if (_quantityDialogController != null)
                _quantityDialogController.QuantityChanged -= OnQuantityChanged;
        }

        private void OpenQuantityDialog()
        {
            if (IsCraftInteractionLocked)
                return;

            if (!ValidateQuantityDialogReferences())
                return;

            int max = GetMaximumCraftable();
            var recipe = _selectionState.Recipe;
            if (recipe == null)
                return;

            if (max <= 0)
            {
                PlayMissingMaterialsWarning();
                return;
            }

            _selectionState.SetQuantity(
                Mathf.Clamp(_selectionState.Quantity, 1, Mathf.Max(1, max))
            );
            _quantityDialogController.Show(
                recipe.resultItem?.icon,
                recipe.resultItem?.itemName,
                1,
                max,
                _selectionState.Quantity,
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
            if (IsCraftInteractionLocked)
                return;

            _selectionState.SetQuantity(quantity);
            StartCraft();
        }

        private void OnQuantityChanged(int quantity)
        {
            _selectionState.SetQuantity(quantity);
            RefreshMaterialRows();
        }

        private int GetMaximumCraftable()
        {
            return _availabilityCalculator.GetMaximumCraftable(
                _selectionState.Recipe,
                GetInventorySnapshot(),
                GetQuickFoodSnapshot()
            );
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Quantity Dialog References")]
        private void AutoAssignQuantityDialogReferences()
        {
            _quantityDialogController ??= GetComponentInChildren<CraftQuantityDialog>(true);
        }
#endif
    }
}
