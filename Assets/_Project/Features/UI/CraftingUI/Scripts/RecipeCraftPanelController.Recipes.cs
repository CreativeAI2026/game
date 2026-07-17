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
            if (!isActiveAndEnabled)
                return;

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
            category = default;
            if (_categoryTabGroup == null || _categoryTabGroup.CurrentIndex < 0)
                return false;

            var definition = _categoryTabGroup.CurrentDefinition;
            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                category = inventoryDefinition.Category;
                return true;
            }

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
            _materialListView?.Clear();
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
            SelectRecipe(recipe);
        }

        private void OnRecipeDoubleClicked(CraftRecipeData recipe)
        {
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
            _selectedRecipe = recipe;
            _recipeListView?.SelectRecipe(recipe);
            _detailPanel?.Show(_selectedRecipe?.resultItem, NoRecipeLabel);
            RebuildMaterialRows();
        }

        private void SelectInitialRecipe(bool forceEmptyLabelRefresh = false)
        {
            var firstRecipe = _recipeListView?.FirstRecipe;
            if (firstRecipe != null)
            {
                SelectRecipe(firstRecipe);
                return;
            }

            _selectedRecipe = null;
            _recipeListView?.SelectRecipe(null);
            _detailPanel?.Show(null, NoRecipeLabel, forceEmptyLabelRefresh);
            RebuildMaterialRows();
        }

        private void RebuildMaterialRows()
        {
            if (_materialListView == null)
                return;

            if (!CanShowSelectedRecipeMaterials())
            {
                _materialListView.Clear();
                return;
            }

            _materialListView.ShowMaterials(_selectedRecipe, _quantity);
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
            return _selectedRecipe != null
                && GetMaximumCraftable() <= 0
                && (
                    InventoryManager.Instance?.HasEquippedMaterial(_selectedRecipe.Materials)
                    ?? false
                );
        }

        private bool HasQuickFoodRecipeMaterial()
        {
            return _selectedRecipe != null
                && GetMaximumCraftable() <= 0
                && (
                    InventoryManager.Instance?.HasQuickFoodMaterial(_selectedRecipe.Materials)
                    ?? false
                );
        }

        private bool CanShowSelectedRecipeMaterials()
        {
            return _selectedRecipe != null
                && _selectedRecipe.resultItem != null
                && (_recipeDB?.IsVisible(_selectedRecipe) ?? false);
        }
    }
}
