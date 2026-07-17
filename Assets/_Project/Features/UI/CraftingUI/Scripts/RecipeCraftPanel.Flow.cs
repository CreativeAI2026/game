using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void StartCraft()
        {
            if (_isCrafting || _selectedRecipe == null)
                return;

            if (HasEquippedRecipeMaterial())
            {
                RebuildMaterialRows();
                PlayEquippedMaterialWarning();
                return;
            }

            if (!(InventoryManager.Instance?.CanCraft(_selectedRecipe, _quantity) ?? false))
            {
                RebuildMaterialRows();
                PlayMissingMaterialsWarning();
                return;
            }

            _craftedRecipeForResult = _selectedRecipe;
            _craftedQuantityForResult = _quantity;

            CloseQuantityDialog();
            HideWarningImmediately();
            _craftRoutine = StartCoroutine(
                CraftRoutine(_craftedRecipeForResult, _craftedQuantityForResult)
            );
        }

        private IEnumerator CraftRoutine(CraftRecipeData recipe, int quantity)
        {
            _isCrafting = true;
            HideWarningImmediately();
            _craftPanel?.ShowLoading();

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            bool crafted =
                recipe != null && (InventoryManager.Instance?.TryCraft(recipe, quantity) ?? false);
            CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);
            _craftPanel?.HideLoading();

            if (!crafted)
            {
                _craftPanel?.HideLoadingAndResult();
                RebuildMaterialRows();
                PlayMissingMaterialsWarning();
                yield break;
            }

            ShowResultPanel();
            RebuildMaterialRows();
        }

        private void ShowResultPanel()
        {
            HideWarningImmediately();
            _craftPanel?.ShowResult(
                _craftedRecipeForResult?.resultItem ?? _selectedRecipe?.resultItem,
                Mathf.Max(1, _craftedQuantityForResult),
                CloseResult
            );
        }

        private void CloseResult()
        {
            SelectRecipeSlot(_slots.FirstOrDefault(slot => slot?.Recipe == _selectedRecipe));
            RebuildMaterialRows();
        }

        private void StopCraftRoutine()
        {
            CraftFlowViewUtility.StopCraftRoutine(this, ref _craftRoutine, ref _isCrafting);
        }
    }
}
