using System.Reflection;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class FreeCraftCraftedItemPreviewTests
    {
        private GameObject _recipeBookObject;
        private RecipeBookManager _recipeBook;
        private CraftRecipeData _recipe;
        private ItemData _result;

        [SetUp]
        public void SetUp()
        {
            TestReflection.SetStaticProperty<RecipeBookManager>("Instance", null);
            _recipeBookObject = new GameObject("RecipeBook");
            _recipeBook = _recipeBookObject.AddComponent<RecipeBookManager>();
            _recipe = ScriptableObject.CreateInstance<CraftRecipeData>();
            _result = ScriptableObject.CreateInstance<ItemData>();
            _result.id = 987654;
            _recipe.resultItem = _result;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_recipe);
            Object.DestroyImmediate(_result);
            Object.DestroyImmediate(_recipeBookObject);
            TestReflection.SetStaticProperty<RecipeBookManager>("Instance", null);
        }

        [Test]
        public void UnknownRecipe_HidesResultItem()
        {
            Assert.IsFalse(IsCraftedItemKnown(_recipe, _recipeBook));
        }

        [Test]
        public void RevealedRecipe_ShowsResultItem()
        {
            Assert.IsTrue(_recipeBook.Reveal(_recipe));

            Assert.IsTrue(IsCraftedItemKnown(_recipe, _recipeBook));
        }

        [Test]
        public void MissingRecipe_HidesResultItem()
        {
            Assert.IsFalse(IsCraftedItemKnown(null, _recipeBook));
        }

        private static bool IsCraftedItemKnown(CraftRecipeData recipe, RecipeBookManager recipeBook)
        {
            MethodInfo method = typeof(FreeCraftPanelController).GetMethod(
                "IsCraftedItemKnown",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { recipe, recipeBook });
        }
    }
}
