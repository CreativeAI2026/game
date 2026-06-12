using System.Collections.Generic;

namespace CreativeAI.Crafting
{
    /// <summary>
    /// インメモリのカタログ実装(仮データ用)。
    /// SQLite 版が出来るまでの代用であり、テストのフィクスチャにも使う。
    /// 同一 ICraftingCatalog を実装するので本番実装と差し替え可能。
    /// </summary>
    public sealed class InMemoryCraftingCatalog : ICraftingCatalog
    {
        private readonly Dictionary<RecipeHash, int> _recipes = new();

        /// <summary>ペア(A,B)の結果を登録する。順不同で引ける。</summary>
        public InMemoryCraftingCatalog Add(int itemIdA, int itemIdB, int resultItemId)
        {
            _recipes[RecipeHash.Of(itemIdA, itemIdB)] = resultItemId;
            return this;
        }

        public bool TryGetResult(RecipeHash recipe, out int resultItemId) =>
            _recipes.TryGetValue(recipe, out resultItemId);

        public int Count => _recipes.Count;
    }
}
