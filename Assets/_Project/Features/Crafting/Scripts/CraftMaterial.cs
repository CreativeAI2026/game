namespace CreativeAI.Crafting
{
    /// <summary>調合のカテゴリ(documents/CraftingArchitecture.md「全体像」)。武器は調合不可。</summary>
    public enum CraftCategory
    {
        Equipment, // 装備品
        Food, // 食材
        Weapon, // 武器(調合対象外)
    }

    /// <summary>
    /// 調合の入力素材。ItemData(ScriptableObject)から切り離した純データ。
    /// 境界で ItemData → CraftMaterial に変換することで、調合の核を
    /// Unity 非依存に保ちテスト可能にする。
    /// </summary>
    public sealed class CraftMaterial
    {
        public int ItemId { get; }
        public CraftCategory Category { get; }
        public StatVector Stats { get; }

        public CraftMaterial(int itemId, CraftCategory category, StatVector stats)
        {
            ItemId = itemId;
            Category = category;
            Stats = stats ?? StatVector.Empty;
        }
    }
}
