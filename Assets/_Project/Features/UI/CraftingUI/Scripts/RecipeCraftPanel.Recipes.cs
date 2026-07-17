using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void BindCategoryTabs()
        {
            ResolveMainReferences();
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

            var definition = _categoryTabGroup.GetDefinitionForButtonIndex(
                _categoryTabGroup.CurrentIndex
            );
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
                $"{nameof(RecipeCraftPanel)} on {name}: Category TabEntry must use {nameof(InventoryTabDefinition)}. Current definition: {(definition != null ? definition.name : "None")}. Recipe list will remain empty.",
                this
            );
        }

        private void PrepareInitialHiddenTemplates()
        {
            HideInactiveRecipeSlots();
            CacheMaterialRows();
            HideMaterialRows();
        }

        private void BuildRecipeList()
        {
            ResolveMainReferences();
            if (_recipeContent == null)
                return;

            UnbindSlots();

            var recipes = GetVisibleRecipes().ToList();
            if (_recipeSlotPrefab != null)
                BuildRecipeListFromPrefab(recipes);
            else
                BindExistingRecipeSlots(recipes.Count);

            if (_recipeContent is RectTransform contentRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }

        private void BuildRecipeListFromPrefab(IEnumerable<CraftRecipeData> recipes)
        {
            ClearGeneratedRecipeSlots();

            if (_recipeSlotPrefab.GetComponent<RecipeSlot>() == null)
            {
                WarnMissingRecipeSlotComponentOnce();
                return;
            }

            int slotIndex = 0;
            foreach (var recipe in recipes)
            {
                var slotObject = Instantiate(_recipeSlotPrefab, _recipeContent, false);
                slotObject.name = _recipeSlotPrefab.name;
                slotObject.SetActive(true);

                var slot = slotObject.GetComponent<RecipeSlot>();
                if (slot == null)
                {
                    WarnMissingRecipeSlotComponentOnce();
                    Destroy(slotObject);
                    continue;
                }

                _generatedRecipeSlots.Add(slot);
                slot.SetRecipe(recipe);
                BindSlot(slot);
                CraftUIAnimationUtility.PlayPopIn(slotObject, slotIndex * 0.04f);
                slotIndex++;
            }
        }

        private void WarnMissingRecipeSlotComponentOnce()
        {
            if (_warnedMissingRecipeSlotPrefab)
                return;

            Debug.LogWarning(
                $"{nameof(RecipeCraftPanel)} on {name}: RecipeSlotPrefab '{_recipeSlotPrefab.name}' のRootに {nameof(RecipeSlot)} がありません。Prefabに追加してください。レシピスロットの生成をスキップします。",
                this
            );
            _warnedMissingRecipeSlotPrefab = true;
        }

        private void ClearGeneratedRecipeSlots()
        {
            foreach (var slot in _generatedRecipeSlots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnRecipeClicked;
                slot.DoubleClicked -= OnRecipeDoubleClicked;
                slot.SetSelected(false);
                Destroy(slot.gameObject);
            }

            foreach (var slot in _recipeContent.GetComponentsInChildren<RecipeSlot>(true))
            {
                if (slot == null || _generatedRecipeSlots.Contains(slot))
                    continue;

                slot.Clicked -= OnRecipeClicked;
                slot.DoubleClicked -= OnRecipeDoubleClicked;
                slot.SetSelected(false);
                slot.gameObject.SetActive(false);
            }

            _generatedRecipeSlots.Clear();
            HideRecipeMaterialSlotTemplates();
        }

        private void BindExistingRecipeSlots(int visibleRecipeCount)
        {
            int boundCount = 0;
            foreach (var slot in _recipeContent.GetComponentsInChildren<RecipeSlot>(true))
            {
                bool isVisible =
                    slot != null
                    && slot.Recipe != null
                    && _recipeDB != null
                    && _recipeDB.IsVisible(slot.Recipe)
                    && IsRecipeInCurrentTab(slot.Recipe);

                if (slot != null)
                    slot.gameObject.SetActive(isVisible);

                if (!isVisible)
                    continue;

                BindSlot(slot);
                boundCount++;
            }

            if (visibleRecipeCount > 0 && boundCount == 0 && !_warnedMissingRecipeSlotPrefab)
            {
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanel)} on {name}: 表示可能なレシピはありますが、RecipeSlotPrefab が未設定で、既存 RecipeSlot も見つかりません。RecipeSlotPrefab を Inspector に設定してください。",
                    this
                );
                _warnedMissingRecipeSlotPrefab = true;
            }
        }

        private void HideInactiveRecipeSlots()
        {
            if (_recipeContent == null)
                return;

            foreach (var slot in _recipeContent.GetComponentsInChildren<RecipeSlot>(true))
            {
                if (slot == null)
                    continue;

                bool isGenerated = _generatedRecipeSlots.Contains(slot);
                bool isVisibleExisting =
                    slot.Recipe != null
                    && _recipeDB != null
                    && _recipeDB.IsVisible(slot.Recipe)
                    && IsRecipeInCurrentTab(slot.Recipe);

                slot.gameObject.SetActive(isGenerated || isVisibleExisting);
            }

            HideRecipeMaterialSlotTemplates();
        }

        private void HideRecipeMaterialSlotTemplates()
        {
            if (_recipeContent == null)
                return;

            foreach (var materialSlot in _recipeContent.GetComponentsInChildren<MaterialSlot>(true))
            {
                if (materialSlot != null)
                    materialSlot.gameObject.SetActive(false);
            }
        }

        private void BindSlot(RecipeSlot slot)
        {
            if (slot == null || _slots.Contains(slot))
                return;

            slot.Clicked -= OnRecipeClicked;
            slot.DoubleClicked -= OnRecipeDoubleClicked;
            slot.Clicked += OnRecipeClicked;
            slot.DoubleClicked += OnRecipeDoubleClicked;
            _slots.Add(slot);
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnRecipeClicked;
                slot.DoubleClicked -= OnRecipeDoubleClicked;
            }

            _slots.Clear();
        }

        private IEnumerable<CraftRecipeData> GetVisibleRecipes()
        {
            ResolveRecipeDB();
            if (_recipeDB != null)
                return _recipeDB.VisibleRecipes.Where(IsRecipeInCurrentTab);

            return Enumerable.Empty<CraftRecipeData>();
        }

        private void OnRecipeClicked(RecipeSlot slot)
        {
            SelectRecipeSlot(slot);
        }

        private void OnRecipeDoubleClicked(RecipeSlot slot)
        {
            SelectRecipeSlot(slot);

            if (HasEquippedRecipeMaterial())
            {
                CloseQuantityDialogImmediately();
                PlayEquippedMaterialWarning();
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

        private void SelectRecipeSlot(RecipeSlot selectedSlot)
        {
            var selectedRecipe = selectedSlot?.Recipe;
            if (_selectedRecipe == selectedRecipe && selectedRecipe != null)
            {
                foreach (var slot in _slots)
                    slot?.SetSelected(slot == selectedSlot);

                _detailPanel?.Show(_selectedRecipe.resultItem, NoRecipeLabel);
                return;
            }

            _selectedRecipe = selectedRecipe;

            foreach (var slot in _slots)
                slot?.SetSelected(slot == selectedSlot);

            _detailPanel?.Show(_selectedRecipe?.resultItem, NoRecipeLabel);
            RebuildMaterialRows();
        }

        private void SelectInitialRecipe(bool forceEmptyLabelRefresh = false)
        {
            var firstSlot = _slots.FirstOrDefault(slot =>
                slot != null && slot.Recipe != null && slot.Recipe.resultItem != null
            );

            if (firstSlot != null)
            {
                SelectRecipeSlot(firstSlot);
                return;
            }

            _selectedRecipe = null;
            foreach (var slot in _slots)
                slot?.SetSelected(false);

            _detailPanel?.Show(null, NoRecipeLabel, forceEmptyLabelRefresh);
            RebuildMaterialRows();
        }

        private void RebuildMaterialRows()
        {
            ResolveMainReferences();
            CacheMaterialRows();

            if (_materialList == null)
                return;

            HideMaterialRows();

            if (!CanShowSelectedRecipeMaterials())
                return;

            var materials = _selectedRecipe.Materials.Where(material => material != null).ToList();
            if (materials.Count == 0)
                return;

            if (_materialRows.Count == 0)
            {
                if (!_warnedMissingMaterialRows)
                {
                    Debug.LogWarning(
                        $"{nameof(RecipeCraftPanel)} on {name}: MaterialList の中に RecipeMaterialRow が見つかりません。MaterialList 配下に2つ配置してください。",
                        this
                    );
                    _warnedMissingMaterialRows = true;
                }
                return;
            }

            _materialList.gameObject.SetActive(true);

            for (int i = 0; i < _materialRows.Count; i++)
            {
                var row = _materialRows[i];
                if (row == null)
                    continue;

                bool hasMaterial = i < materials.Count;
                row.gameObject.SetActive(hasMaterial);

                if (!hasMaterial)
                    continue;

                row.Show(materials[i]);
                CraftUIAnimationUtility.PlayRowIn(row.gameObject, i);
            }

            if (_materialList is RectTransform materialRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(materialRect);
            }
        }

        private void CacheMaterialRows()
        {
            _materialRows.Clear();

            if (_materialList == null)
                return;

            _materialRows.AddRange(
                _materialList
                    .GetComponentsInChildren<RecipeMaterialRow>(true)
                    .Where(row => row != null)
                    .OrderBy(row => row.transform.GetSiblingIndex())
            );
        }

        private void HideMaterialRowTemplate()
        {
            HideMaterialRows();
        }

        private void HideMaterialRows()
        {
            if (_materialList == null)
                return;

            _materialList.gameObject.SetActive(false);

            if (_materialRows.Count == 0)
                CacheMaterialRows();

            foreach (var row in _materialRows)
            {
                if (row != null)
                    row.gameObject.SetActive(false);
            }
        }

        private void SubscribeRecipeDBChanges()
        {
            ResolveRecipeDB();
            if (_subscribedRecipeDB == _recipeDB)
                return;

            UnsubscribeRecipeDBChanges();

            if (_recipeDB == null)
                return;

            _recipeDB.RecipeRevealed += OnRecipeRevealed;
            _subscribedRecipeDB = _recipeDB;
        }

        private void UnsubscribeRecipeDBChanges()
        {
            if (_subscribedRecipeDB == null)
                return;

            _subscribedRecipeDB.RecipeRevealed -= OnRecipeRevealed;
            _subscribedRecipeDB = null;
        }

        private void OnRecipeRevealed(CraftRecipeData recipe)
        {
            if (!isActiveAndEnabled)
                return;

            ResolveAllReferences();
            BuildRecipeList();
            SelectRecipeSlot(
                _slots.FirstOrDefault(slot => slot != null && slot.Recipe == recipe)
                    ?? _slots.FirstOrDefault(slot =>
                        slot != null && slot.Recipe != null && slot.Recipe.resultItem != null
                    )
            );
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

        private bool CanShowSelectedRecipeMaterials()
        {
            return _selectedRecipe != null
                && _selectedRecipe.resultItem != null
                && (_recipeDB?.IsVisible(_selectedRecipe) ?? false);
        }
    }
}
