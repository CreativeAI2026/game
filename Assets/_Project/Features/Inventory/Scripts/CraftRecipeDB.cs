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

        public IReadOnlyList<CraftRecipeData> Recipes => _recipes;

        /// <summary>非表示だったレシピが新規解禁されたときに発火(UIが一覧を組み直す)。</summary>
        public event Action<CraftRecipeData> RecipeRevealed;

        // 解禁(発見)状態は保持しない。セッション常駐でセーブ対象の RecipeBookManager が唯一の持ち主。
        // カタログ(この SO)は読み取り専用に徹し、表示判定は全て RecipeBookManager へ委譲する。
        // 初期解禁(showInRecipeCraft)は静的な設計データで、RecipeBookManager が起動時に取り込む
        // (documents/Specification.md §2.3「データ形式」/ §6)。
        private static bool IsRecipeRevealed(CraftRecipeData recipe) =>
            RecipeBookManager.Instance?.IsRevealed(recipe) ?? false;

        public IEnumerable<CraftRecipeData> VisibleRecipes =>
            _recipes.Where(recipe =>
                recipe != null && recipe.resultItem != null && IsVisible(recipe)
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

            bool wasHidden = !IsRecipeRevealed(recipe);
            bool newlyRevealed = RecipeBookManager.Instance?.Reveal(recipe) ?? false;
            if (wasHidden && newlyRevealed)
                RecipeRevealed?.Invoke(recipe);

            return wasHidden && newlyRevealed;
        }

        public bool IsVisible(CraftRecipeData recipe)
        {
            return recipe != null && recipe.resultItem != null && IsRecipeRevealed(recipe);
        }
    }
}
