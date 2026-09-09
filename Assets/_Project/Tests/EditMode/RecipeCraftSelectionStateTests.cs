using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// レシピ調合の選択レシピと個数の保持・クランプの検証。
    /// </summary>
    public class RecipeCraftSelectionStateTests
    {
        [Test]
        public void InitialState_HasNoSelectionAndQuantityOne()
        {
            var state = new RecipeCraftSelectionState();

            Assert.IsFalse(state.HasCategory);
            Assert.IsNull(state.Recipe);
            Assert.AreEqual(1, state.Quantity);
        }

        [Test]
        public void SelectionAndQuantity_AreStoredTogether()
        {
            var state = new RecipeCraftSelectionState();
            var recipe = ScriptableObject.CreateInstance<CraftRecipeData>();

            try
            {
                state.SelectCategory(ItemCategory.Food);
                state.SelectRecipe(recipe);
                state.SetQuantity(3);

                Assert.IsTrue(state.HasCategory);
                Assert.AreEqual(ItemCategory.Food, state.Category);
                Assert.AreSame(recipe, state.Recipe);
                Assert.AreEqual(3, state.Quantity);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
            }
        }

        [Test]
        public void SetQuantity_ClampsToOne()
        {
            var state = new RecipeCraftSelectionState();

            state.SetQuantity(0);

            Assert.AreEqual(1, state.Quantity);
        }

        [Test]
        public void Reset_ClearsSelectionAndRestoresQuantity()
        {
            var state = new RecipeCraftSelectionState();
            var recipe = ScriptableObject.CreateInstance<CraftRecipeData>();

            try
            {
                state.SelectCategory(ItemCategory.Equipment);
                state.SelectRecipe(recipe);
                state.SetQuantity(4);

                state.Reset();

                Assert.IsFalse(state.HasCategory);
                Assert.IsNull(state.Recipe);
                Assert.AreEqual(1, state.Quantity);
            }
            finally
            {
                Object.DestroyImmediate(recipe);
            }
        }
    }
}
