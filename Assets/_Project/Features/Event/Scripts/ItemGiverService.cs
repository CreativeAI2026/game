namespace CreativeAI.Gameplay
{
    /// <summary>
    /// giveItem ステップと hasItem 条件の届け先。実体は InventoryManager で、プレビュー・テストは差し替える。
    /// </summary>
    public interface IItemGiver
    {
        void Give(string itemKey);

        /// <summary>
        /// itemKey の「大事なもの」を1つ以上所持しているか(hasItem 条件の判定)。
        /// 実体(InventoryManager)は itemKey を ItemCatalog で引き、カテゴリが 大事なもの の場合のみ
        /// 所持数を見る。装備品/食材/武器のキーや未登録キーは対象外で false を返す。
        /// </summary>
        bool HasImportantItem(string itemKey);
    }

    /// <summary>
    /// 実行時の IItemGiver を登録する窓口。InventoryManager が Awake で登録し、
    /// EventPlayer と hasItem 条件の判定(<see cref="EventAvailability"/>)が見る。テストは偽物を差し込める。
    /// </summary>
    public static class ItemGiverService
    {
        public static IItemGiver Current { get; set; }
    }
}
