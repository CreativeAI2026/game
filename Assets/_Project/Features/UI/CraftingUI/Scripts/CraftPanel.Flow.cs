using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private void FindCraftFlowReferences()
        {
            _craftButton ??= FindDescendant("CraftButton")?.GetComponent<Button>();
            _loadingPanel ??= FindDescendant("LoadingPanel")?.gameObject;
            _loadingGear ??= FindDescendant("LoadingGear") as RectTransform;
            _resultPanel ??= FindDescendant("ResultPanel")?.gameObject;
            if (_resultPanel != null)
            {
                _resultItemImage ??= FindComponentIn<Image>(_resultPanel.transform, "ItemImage");
                _resultItemName ??= FindComponentIn<TMP_Text>(_resultPanel.transform, "ItemName");
            }
            _closeButton ??= FindDescendant("CloseButton")?.gameObject;
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);
            UIButtonHoverScaleUtility.ApplyTo(_closeButton?.GetComponent<Button>());
        }

        private void BindCraftFlow()
        {
            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveListener(StartCraft);
                _craftButton.onClick.AddListener(StartCraft);
            }

            if (_resultPanel == null)
                return;

            _resultClickCatcher = CraftFlowViewUtility.PrepareClickCatcher(
                _resultPanel,
                CloseResult
            );
        }

        private void UpdateCraftButton()
        {
            bool hasEnoughMaterials = HasEnoughMaterials();
            bool hasCategoryMismatch = HasCategoryMismatch();
            bool hasEquippedMaterial = HasEquippedMaterial();
            bool hasRecipe = FindSelectedRecipe() != null;
            bool canCraft = hasRecipe && CanCraft();

            if (_craftButton != null)
                _craftButton.interactable = !_isCrafting && canCraft;

            if (_isCrafting || canCraft)
                HideWarning();
            else if (hasEquippedMaterial)
                ShowEquippedMaterialWarning();
            else if (!hasEnoughMaterials)
                HideWarning();
            else if (hasCategoryMismatch)
                ShowCategoryMismatchWarning();
        }

        private void StartCraft()
        {
            if (_isCrafting)
                return;

            if (!CanCraft())
            {
                if (HasEquippedMaterial())
                    ShowEquippedMaterialWarning();
                else if (HasCategoryMismatch())
                    ShowCategoryMismatchWarning();
                else
                    ShowNotReadyWarning();

                return;
            }

            StopCraftRoutine();
            _craftRoutine = StartCoroutine(CraftRoutine());
        }

        private bool CanCraft()
        {
            var recipe = FindSelectedRecipe();
            return recipe != null
                && (
                    InventoryManager.Instance?.CanCraft(
                        recipe,
                        GetMaterialStack(0),
                        GetMaterialStack(1)
                    )
                    ?? false
                );
        }

        private bool HasEnoughMaterials()
        {
            return _slots.Count(slot => slot.Stack != null) >= 2;
        }

        private bool HasCategoryMismatch()
        {
            var selectedItems = _slots
                .Where(slot => slot.Stack?.Data != null)
                .Select(slot => slot.Stack.Data)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return false;

            return selectedItems[0].category != selectedItems[1].category;
        }

        private bool HasEquippedMaterial()
        {
            return _slots
                .Where(slot => slot.Stack != null)
                .Select(slot => slot.Stack)
                .Take(2)
                .Any(stack => stack.IsEquipped);
        }

        private IEnumerator CraftRoutine()
        {
            _isCrafting = true;
            _lastCraftedRecipe = FindSelectedRecipe();
            UpdateCraftButton();

            SetCloseButtonVisible(false);
            HideWarning();

            CraftFlowViewUtility.ShowLoading(_loadingPanel, _loadingGear, _resultPanel);

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            bool crafted =
                _lastCraftedRecipe != null
                && (
                    InventoryManager.Instance?.TryCraft(
                        _lastCraftedRecipe,
                        GetMaterialStack(0),
                        GetMaterialStack(1)
                    )
                    ?? false
                );
            if (crafted)
                _recipeDB?.RevealRecipe(
                    _lastCraftedRecipe.material1,
                    _lastCraftedRecipe.material2,
                    out _
                );

            CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);

            CraftFlowViewUtility.HideLoadingGear(_loadingGear);
            CraftFlowViewUtility.HideLoadingPanel(_loadingPanel);
            if (!crafted)
            {
                CraftFlowViewUtility.HidePanels(_loadingPanel, null);
                SetCloseButtonVisible(true);
                ShowNotReadyWarning();
                UpdateCraftButton();
                yield break;
            }

            if (_resultPanel != null)
            {
                HideWarning();
                RefreshResultPanel();
                _resultClickCatcher?.SetClickAction(CloseResult);
                CraftUIAnimationUtility.PlayResultIn(_resultPanel);
            }

            UpdateCraftButton();
        }

        private CraftRecipeData FindSelectedRecipe()
        {
            if (_recipeDB == null)
                return null;

            var selectedItems = _slots
                .Where(slot => slot.Stack?.Data != null)
                .Select(slot => slot.Stack.Data)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return null;

            return _recipeDB.FindRecipe(selectedItems[0], selectedItems[1]);
        }

        private ItemStack GetMaterialStack(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index].Stack : null;
        }

        private void RefreshResultPanel()
        {
            FindCraftFlowReferences();
            var resultItem = _lastCraftedRecipe?.resultItem;
            CraftFlowViewUtility.RefreshResult(_resultItemImage, _resultItemName, resultItem, 1);
        }

        private void CloseResult()
        {
            CraftFlowViewUtility.HidePanels(_loadingPanel, _resultPanel);
            HideWarning();
            SetCloseButtonVisible(true);
            ResetSlots();
            SelectFirstSlotIfNeeded();
        }

        private void ResetCraftFlow()
        {
            StopCraftRoutine();
            _lastCraftedRecipe = null;
            RefreshResultPanel();

            CraftFlowViewUtility.HidePanels(_loadingPanel, _resultPanel);
            HideWarning();
            if (_loadingGear != null)
                _loadingGear.localRotation = Quaternion.identity;

            SetCloseButtonVisible(true);
            UpdateCraftButton();
        }

        private void SetCloseButtonVisible(bool visible)
        {
            CraftFlowViewUtility.SetCloseButtonVisible(_closeButton, visible);
        }

        private void StopCraftRoutine()
        {
            CraftFlowViewUtility.StopCraftRoutine(this, ref _craftRoutine, ref _isCrafting);
        }
    }
}
