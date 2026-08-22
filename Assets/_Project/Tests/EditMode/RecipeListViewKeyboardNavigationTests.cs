using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// レシピ一覧のキーボード移動と決定の検証。
    /// </summary>
    public class RecipeListViewKeyboardNavigationTests
    {
        private GameObject _root;
        private RecipeListView _view;
        private readonly List<CraftRecipeData> _recipes = new();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RecipeListView");
            _view = _root.AddComponent<RecipeListView>();
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(_root.transform);
            var grid = contentObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            TestReflection.SetField(_view, "_content", contentObject.transform);

            var slots = TestReflection.GetField<List<RecipeSlot>>(_view, "_slots");
            for (int i = 0; i < 8; i++)
            {
                var recipe = ScriptableObject.CreateInstance<CraftRecipeData>();
                var item = ScriptableObject.CreateInstance<ItemData>();
                item.id = 9000 + i;
                recipe.resultItem = item;
                _recipes.Add(recipe);

                var slotObject = new GameObject($"RecipeSlot{i}");
                slotObject.transform.SetParent(contentObject.transform);
                var slot = slotObject.AddComponent<RecipeSlot>();
                slot.SetRecipe(recipe);
                slots.Add(slot);
            }

            _view.RecipeClicked += _view.SelectRecipe;
            _view.SelectRecipe(_recipes[0]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CraftRecipeData recipe in _recipes)
            {
                Object.DestroyImmediate(recipe.resultItem);
                Object.DestroyImmediate(recipe);
            }
            _recipes.Clear();
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void RightMove_SelectsNextRecipe()
        {
            CraftRecipeData selected = null;
            _view.RecipeClicked += recipe => selected = recipe;

            TestReflection.Invoke(_view, "SelectByOffset", 1);

            Assert.AreSame(_recipes[1], selected);
        }

        [Test]
        public void DownMove_SelectsRecipeInNextGridRow()
        {
            CraftRecipeData selected = null;
            _view.RecipeClicked += recipe => selected = recipe;

            TestReflection.Invoke(_view, "SelectVertically", 1);

            Assert.AreSame(_recipes[6], selected);
        }

        [Test]
        public void SubmitSelectedRecipe_UsesSameEventAsMouseDoubleClick()
        {
            CraftRecipeData submitted = null;
            _view.RecipeDoubleClicked += recipe => submitted = recipe;

            _view.SubmitSelectedRecipe();

            Assert.AreSame(_recipes[0], submitted);
        }
    }
}
