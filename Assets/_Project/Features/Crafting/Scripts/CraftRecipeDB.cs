using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>レシピ1件。素材2つ(順不同)から完成品が1つ決まる。</summary>
    [Serializable]
    public class CraftRecipe
    {
        public ItemData material1;
        public ItemData material2;
        public ItemData resultItem;

        // 初期から解禁(常時表示)かどうかの設計データ。実行時の解禁状態は RecipeBookManager が持つ。
        public bool showInRecipeCraft;

        public IEnumerable<ItemData> Materials =>
            new[] { material1, material2 }.Where(item => item != null);

        public bool MatchesMaterials(ItemData itemA, ItemData itemB)
        {
            if (itemA == null || itemB == null || material1 == null || material2 == null)
                return false;

            return (material1 == itemA && material2 == itemB)
                || (material1 == itemB && material2 == itemA);
        }
    }

    /// <summary>
    /// レシピの一覧。(素材A, 素材B) → レシピ の Map として引ける。読み取り専用のカタログで、
    /// 解禁状態は持たない(RecipeBookManager に問い合わせる)。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CraftRecipeDB",
        menuName = "Scriptable Objects/Crafting/Craft Recipe DB"
    )]
    public class CraftRecipeDB : ScriptableObject
    {
        [SerializeField]
        private List<CraftRecipe> _recipes = new();

        private Dictionary<(int, int), CraftRecipe> _recipeByMaterials;

        public IReadOnlyList<CraftRecipe> Recipes => _recipes;

        public IEnumerable<CraftRecipe> VisibleRecipes => _recipes.Where(IsVisible);

        public CraftRecipe FindRecipe(ItemData materialA, ItemData materialB)
        {
            if (materialA == null || materialB == null)
                return null;

            _recipeByMaterials ??= BuildMap();
            return _recipeByMaterials.GetValueOrDefault(MaterialKey(materialA, materialB));
        }

        public bool IsVisible(CraftRecipe recipe)
        {
            return recipe?.resultItem != null
                && (RecipeBookManager.Instance?.IsRevealed(recipe) ?? false);
        }

        private Dictionary<(int, int), CraftRecipe> BuildMap()
        {
            var map = new Dictionary<(int, int), CraftRecipe>();
            foreach (var recipe in _recipes)
            {
                if (recipe?.material1 != null && recipe.material2 != null)
                    map.TryAdd(MaterialKey(recipe.material1, recipe.material2), recipe);
            }
            return map;
        }

        // 素材の並び順を問わないよう、小さい方を先にしたキーにする。
        private static (int, int) MaterialKey(ItemData a, ItemData b)
        {
            int idA = a.GetInstanceID();
            int idB = b.GetInstanceID();
            return idA < idB ? (idA, idB) : (idB, idA);
        }

        private void OnValidate()
        {
            _recipeByMaterials = null;
        }

#if UNITY_EDITOR
        /// <summary>CSV 取り込み用。同じ完成品のレシピがあれば置き換え、無ければ追加する。</summary>
        public void SetRecipe(CraftRecipe recipe)
        {
            int index = _recipes.FindIndex(existing => existing?.resultItem == recipe.resultItem);
            if (index >= 0)
                _recipes[index] = recipe;
            else
                _recipes.Add(recipe);
            _recipeByMaterials = null;
        }
#endif
    }
}
