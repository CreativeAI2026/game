using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void ResolveCraftFlowReferences() { }

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
            _craftPanel?.SetCloseButtonVisible(false);
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
                _craftPanel?.SetCloseButtonVisible(true);
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
            _craftPanel?.HideResult();
            HideWarningImmediately();
            _craftPanel?.SetCloseButtonVisible(true);

            SelectRecipeSlot(_slots.FirstOrDefault(slot => slot?.Recipe == _selectedRecipe));
            RebuildMaterialRows();

            var inventory = GetCraftFlowRoot()?.GetComponentInChildren<Inventory>(true);
            inventory?.RefreshCurrentTab();
        }

        private void StopCraftRoutine()
        {
            CraftFlowViewUtility.StopCraftRoutine(this, ref _craftRoutine, ref _isCrafting);
        }
    }
}
