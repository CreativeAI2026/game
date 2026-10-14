using System;

namespace CreativeAI.Gameplay
{
    /// <summary>レシピ1件。素材2つ(順不同)から完成品が1つ決まる。</summary>
    [Serializable]
    public class CraftRecipe
    {
        public ItemData material1;
        public ItemData material2;
        public ItemData resultItem;

        public bool MatchesMaterials(ItemData itemA, ItemData itemB)
        {
            if (itemA == null || itemB == null || material1 == null || material2 == null)
                return false;

            return (material1 == itemA && material2 == itemB)
                || (material1 == itemB && material2 == itemA);
        }
    }
}
