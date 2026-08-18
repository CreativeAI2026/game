using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanelController
    {
        private void BindCategoryTabs()
        {
            if (_categoryTabGroup == null)
                return;

            _categoryTabGroup.OnTabDefinitionSelected -= OnCategoryTabSelected;
            _categoryTabGroup.OnTabDefinitionSelected += OnCategoryTabSelected;
        }

        private void UnbindCategoryTabs()
        {
            if (_categoryTabGroup != null)
                _categoryTabGroup.OnTabDefinitionSelected -= OnCategoryTabSelected;
        }

        private void BindRecipeListView()
        {
            if (_recipeListView == null)
                return;

            _recipeListView.RecipeClicked -= OnRecipeClicked;
            _recipeListView.RecipeDoubleClicked -= OnRecipeDoubleClicked;
            _recipeListView.RecipeClicked += OnRecipeClicked;
            _recipeListView.RecipeDoubleClicked += OnRecipeDoubleClicked;
        }

        private void UnbindRecipeListView()
        {
            if (_recipeListView == null)
                return;

            _recipeListView.RecipeClicked -= OnRecipeClicked;
            _recipeListView.RecipeDoubleClicked -= OnRecipeDoubleClicked;
        }

        private void OnCategoryTabSelected(int _index, TabDefinition _definition)
        {
            if (!isActiveAndEnabled || IsCraftInteractionLocked)
                return;

            if (_definition is InventoryTabDefinition inventoryDefinition)
                _selectionState.SelectCategory(inventoryDefinition.Category);
            else
                _selectionState.ClearCategory();

            BuildRecipeList();
            SelectInitialRecipe(true);
            ForceRebuildLayouts();
        }

        private bool IsRecipeInCurrentTab(CraftRecipeData recipe)
        {
            if (recipe == null || recipe.resultItem == null)
                return false;

            return TryGetCurrentCategory(out var category)
                && recipe.resultItem.category == category;
        }

        private bool TryGetCurrentCategory(out ItemCategory category)
        {
            if (_selectionState.HasCategory)
            {
                category = _selectionState.Category;
                return true;
            }

            category = default;
            if (_categoryTabGroup == null || _categoryTabGroup.CurrentIndex < 0)
                return false;

            var definition = _categoryTabGroup.CurrentDefinition;
            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                category = inventoryDefinition.Category;
                _selectionState.SelectCategory(category);
                return true;
            }

            _selectionState.ClearCategory();
            WarnInvalidCategoryTabOnce(definition);
            return false;
        }

        private void WarnInvalidCategoryTabOnce(TabDefinition definition)
        {
            if (_warnedInvalidCategoryTab)
                return;

            _warnedInvalidCategoryTab = true;
            Debug.LogWarning(
                $"{nameof(RecipeCraftPanelController)} on {name}: Category TabEntry must use {nameof(InventoryTabDefinition)}. Current definition: {(definition != null ? definition.name : "None")}. Recipe list will remain empty.",
                this
            );
        }

        private void PrepareInitialHiddenTemplates()
        {
            _recipeListView?.Clear();
            _materialRowsView?.Clear();
        }

        private void BuildRecipeList()
        {
            _recipeListView?.SetRecipes(GetVisibleRecipes());
        }

        private IEnumerable<CraftRecipeData> GetVisibleRecipes()
        {
            return _recipeDB != null
                ? _recipeDB.VisibleRecipes.Where(IsRecipeInCurrentTab)
                : Enumerable.Empty<CraftRecipeData>();
        }

        private void OnRecipeClicked(CraftRecipeData recipe)
        {
            if (IsCraftInteractionLocked)
                return;

            SelectRecipe(recipe);
        }

        private void OnRecipeDoubleClicked(CraftRecipeData recipe)
        {
            if (IsCraftInteractionLocked)
                return;

            SelectRecipe(recipe);

            if (HasEquippedRecipeMaterial())
            {
                CloseQuantityDialogImmediately();
                PlayEquippedMaterialWarning();
                return;
            }

            if (HasQuickFoodRecipeMaterial())
            {
                CloseQuantityDialogImmediately();
                PlayQuickFoodMaterialWarning();
                return;
            }

            if (GetMaximumCraftable() <= 0)
            {
                CloseQuantityDialogImmediately();
                PlayMissingMaterialsWarning();
                return;
            }

            OpenQuantityDialog();
        }

        private void SelectRecipe(CraftRecipeData recipe)
        {
            _selectionState.SelectRecipe(recipe);
            _recipeListView?.SelectRecipe(recipe);
            _detailPanel?.Show(_selectionState.Recipe?.resultItem, NoRecipeLabel);
            RefreshMaterialRows();
        }

        private void SelectInitialRecipe(bool forceEmptyLabelRefresh = false)
        {
            var firstRecipe = _recipeListView?.FirstRecipe;
            if (firstRecipe != null)
            {
                SelectRecipe(firstRecipe);
                return;
            }

            _selectionState.SelectRecipe(null);
            _recipeListView?.SelectRecipe(null);
            _detailPanel?.Show(null, NoRecipeLabel, forceEmptyLabelRefresh);
            RefreshMaterialRows();
        }

        private void RefreshMaterialRows(bool animate = true)
        {
            if (_materialRowsView == null)
                return;

            if (!CanShowSelectedRecipeMaterials())
            {
                _materialRowsView.Clear();
                return;
            }

            var rows = _availabilityCalculator.BuildMaterialRows(
                _selectionState.Recipe,
                _selectionState.Quantity,
                GetInventorySnapshot()
            );
            _materialRowsView.ShowRows(rows, animate);
        }

        private void SubscribeRecipeBookChanges()
        {
            var recipeBook = RecipeBookManager.Instance;
            if (_subscribedRecipeBook == recipeBook)
                return;

            UnsubscribeRecipeBookChanges();
            if (recipeBook == null)
                return;

            recipeBook.RecipeRevealed += OnRecipeRevealed;
            _subscribedRecipeBook = recipeBook;
        }

        private void UnsubscribeRecipeBookChanges()
        {
            if (_subscribedRecipeBook == null)
                return;

            _subscribedRecipeBook.RecipeRevealed -= OnRecipeRevealed;
            _subscribedRecipeBook = null;
        }

        private void OnRecipeRevealed(CraftRecipeData recipe)
        {
            if (!isActiveAndEnabled)
                return;

            BuildRecipeList();
            SelectRecipe(IsRecipeInCurrentTab(recipe) ? recipe : _recipeListView?.FirstRecipe);
            ForceRebuildLayouts();
        }

        private bool HasEquippedRecipeMaterial()
        {
            return _availabilityCalculator.HasEquippedMaterial(
                _selectionState.Recipe,
                GetInventorySnapshot()
            );
        }

        private bool HasQuickFoodRecipeMaterial()
        {
            return _availabilityCalculator.HasQuickFoodMaterial(
                _selectionState.Recipe,
                1,
                GetInventorySnapshot(),
                GetQuickFoodSnapshot()
            );
        }

        private bool CanShowSelectedRecipeMaterials()
        {
            var recipe = _selectionState.Recipe;
            return recipe != null
                && recipe.resultItem != null
                && (_recipeDB?.IsVisible(recipe) ?? false);
        }
    }
}
