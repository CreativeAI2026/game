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
        private void ResolveCraftFlowReferences()
        {
            var root = GetCraftFlowRoot();

            _loadingPanel ??= FindGameObjectIn(root, "LoadingPanel");
            _loadingGear ??= FindIn(root, "LoadingGear") as RectTransform;
            _resultPanel ??= FindGameObjectIn(root, "ResultPanel");
            _closeButton ??= FindGameObjectIn(root, "CloseButton");

            if (_resultPanel != null)
            {
                _resultItemImage ??= FindComponentIn<Image>(_resultPanel.transform, "ItemImage");
                _resultItemName ??= FindComponentIn<TMP_Text>(_resultPanel.transform, "ItemName");
                _resultClickCatcher ??= CraftFlowViewUtility.PrepareClickCatcher(
                    _resultPanel,
                    CloseResult
                );
            }

            UIButtonHoverScaleUtility.ApplyTo(_closeButton?.GetComponent<Button>());
        }

        private void StartCraft()
        {
            if (_isCrafting || _selectedRecipe == null)
                return;

            SetQuantity(_quantity);

            if (HasEquippedRecipeMaterial())
            {
                RefreshQuantityDialog();
                RebuildMaterialRows();
                PlayEquippedMaterialWarning();
                return;
            }

            if (!(InventoryManager.Instance?.CanCraft(_selectedRecipe, _quantity) ?? false))
            {
                RefreshQuantityDialog();
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
            SetCloseButtonVisible(false);
            HideWarningImmediately();
            CraftFlowViewUtility.ShowLoading(_loadingPanel, _loadingGear, _resultPanel);

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            bool crafted =
                recipe != null && (InventoryManager.Instance?.TryCraft(recipe, quantity) ?? false);
            CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);
            CraftFlowViewUtility.HideLoadingGear(_loadingGear);
            CraftFlowViewUtility.HideLoadingPanel(_loadingPanel);

            if (!crafted)
            {
                CraftFlowViewUtility.HidePanels(_loadingPanel, null);
                SetCloseButtonVisible(true);
                RebuildMaterialRows();
                PlayMissingMaterialsWarning();
                yield break;
            }

            ShowResultPanel();
            RebuildMaterialRows();
        }

        private void ShowResultPanel()
        {
            ResolveCraftFlowReferences();
            if (_resultPanel == null)
                return;

            HideWarningImmediately();
            RefreshResultPanel();
            _resultClickCatcher = CraftFlowViewUtility.PrepareClickCatcher(
                _resultPanel,
                CloseResult
            );
            CraftUIAnimationUtility.PlayResultIn(_resultPanel);
        }

        private void RefreshResultPanel()
        {
            ResolveCraftFlowReferences();
            CraftFlowViewUtility.RefreshResult(
                _resultItemImage,
                _resultItemName,
                _craftedRecipeForResult?.resultItem ?? _selectedRecipe?.resultItem,
                Mathf.Max(1, _craftedQuantityForResult)
            );
        }

        private void CloseResult()
        {
            CraftFlowViewUtility.HidePanels(_loadingPanel, _resultPanel);
            HideWarningImmediately();
            SetCloseButtonVisible(true);

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
