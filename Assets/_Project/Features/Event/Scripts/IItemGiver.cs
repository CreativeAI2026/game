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

        /// <summary>
        /// itemKey のアイテムの所持数(未装備のぶんの合計。全カテゴリ対象)。useItem ステップの正解判定に使う。
        /// </summary>
        int CountItem(string itemKey);

        /// <summary>
        /// itemKey の未装備のアイテムを count 個消費する。足りなければ何も変えず false。
        /// </summary>
        bool Consume(string itemKey, int count);
    }
}
