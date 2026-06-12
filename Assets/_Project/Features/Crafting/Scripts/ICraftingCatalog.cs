namespace CreativeAI.Crafting
{
    /// <summary>
    /// ペア → 結果アイテム の引き(CraftingArchitecture.md「カタログ」)。
    /// 本番は同梱 SQLite(catalog.db)を読む実装、テスト/開発は
    /// インメモリ実装を差し込む。調合の核はこの抽象だけに依存する。
    /// </summary>
    public interface ICraftingCatalog
    {
        /// <summary>
        /// 素材ペアに対応する結果アイテム id を引く。
        /// 有効ペアは事前生成済みなので本番では必ずヒットする。
        /// </summary>
        bool TryGetResult(RecipeHash recipe, out int resultItemId);
    }
}
