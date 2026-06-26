using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(
        fileName = "CraftRecipeDB",
        menuName = "Scriptable Objects/Crafting/Craft Recipe DB"
    )]
    public class CraftRecipeDB : ScriptableObject
    {
        [SerializeField]
        private List<CraftRecipeData> _recipes = new();

        [NonSerialized]
        private readonly HashSet<CraftRecipeData> _runtimeRevealedRecipes = new();

        public IReadOnlyList<CraftRecipeData> Recipes => _recipes;

        private void OnEnable()
        {
            ResetRuntimeRevealedRecipes();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetAllRuntimeRevealedRecipesOnPlayStart()
        {
            foreach (var database in Resources.LoadAll<CraftRecipeDB>("Crafting"))
                database.ResetRuntimeRevealedRecipes();
        }

        public IEnumerable<CraftRecipeData> VisibleRecipes =>
            _recipes.Where(recipe =>
                recipe != null
                && recipe.resultItem != null
                && (recipe.showInRecipeCraft || _runtimeRevealedRecipes.Contains(recipe))
            );

        public CraftRecipeData FindRecipe(ItemData materialA, ItemData materialB)
        {
            return _recipes.FirstOrDefault(recipe =>
                recipe != null && recipe.MatchesMaterials(materialA, materialB)
            );
        }

        public bool RevealRecipe(ItemData materialA, ItemData materialB, out CraftRecipeData recipe)
        {
            recipe = FindRecipe(materialA, materialB);
            if (recipe == null)
                return false;

            bool wasHidden = !recipe.showInRecipeCraft && !_runtimeRevealedRecipes.Contains(recipe);
            _runtimeRevealedRecipes.Add(recipe);
            return wasHidden;
        }

        public bool IsVisible(CraftRecipeData recipe)
        {
            return recipe != null
                && recipe.resultItem != null
                && (recipe.showInRecipeCraft || _runtimeRevealedRecipes.Contains(recipe));
        }

        public void ResetRuntimeRevealedRecipes()
        {
            _runtimeRevealedRecipes.Clear();
        }
    }
}
