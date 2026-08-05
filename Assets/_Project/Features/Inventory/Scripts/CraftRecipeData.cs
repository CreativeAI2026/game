using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(
        fileName = "CraftRecipe",
        menuName = "Scriptable Objects/Crafting/Craft Recipe"
    )]
    public class CraftRecipeData : ScriptableObject
    {
        public ItemData resultItem;
        public ItemData material1;
        public ItemData material2;

        // 初期から解禁(常時表示)かどうかの静的な設計データ。実行時の解禁「状態」ではない
        // (状態は RecipeBookManager が唯一保持し、起動時にこのフラグを取り込む)。
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
}
