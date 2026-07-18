using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanelController
    {
        private void StartCraft()
        {
            var recipe = _selectionState.Recipe;
            int quantity = _selectionState.Quantity;
            if (IsCraftInteractionLocked || recipe == null)
                return;

            var inventorySnapshot = GetInventorySnapshot();
            if (_availabilityCalculator.HasEquippedMaterial(recipe, inventorySnapshot))
            {
                RefreshMaterialRows();
                PlayEquippedMaterialWarning();
                return;
            }

            var quickFoodSnapshot = GetQuickFoodSnapshot();
            if (
                _availabilityCalculator.HasQuickFoodMaterial(
                    recipe,
                    quantity,
                    inventorySnapshot,
                    quickFoodSnapshot
                )
            )
            {
                RefreshMaterialRows();
                PlayQuickFoodMaterialWarning();
                return;
            }

            if (
                !_availabilityCalculator.CanCraft(
                    recipe,
                    quantity,
                    inventorySnapshot,
                    quickFoodSnapshot
                )
            )
            {
                RefreshMaterialRows();
                PlayMissingMaterialsWarning();
                return;
            }

            _craftedRecipeForResult = recipe;
            _craftedQuantityForResult = quantity;

            CloseQuantityDialog();
            HideWarningImmediately();
            _ownsCraftFlow = true;
            _craftRoutine = StartCoroutine(
                CraftRoutine(_craftedRecipeForResult, _craftedQuantityForResult)
            );
        }

        private IEnumerator CraftRoutine(CraftRecipeData recipe, int quantity)
        {
            _isCrafting = true;
            try
            {
                yield return _craftPanel.RunCraftFlow(
                    () =>
                        recipe != null
                        && (InventoryManager.Instance?.TryCraft(recipe, quantity) ?? false),
                    recipe?.resultItem,
                    Mathf.Max(1, _craftedQuantityForResult),
                    CloseResult,
                    PlayMissingMaterialsWarning
                );
            }
            finally
            {
                CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);
                if (!_craftPanel.IsCraftFlowRunning)
                    _ownsCraftFlow = false;
                RefreshMaterialRows();
            }
        }

        private void CloseResult()
        {
            _ownsCraftFlow = false;
            SelectRecipe(_selectionState.Recipe);
            RefreshMaterialRows();
        }

        private void StopCraftRoutine()
        {
            CraftFlowViewUtility.StopCraftRoutine(this, ref _craftRoutine, ref _isCrafting);
        }
    }
}
