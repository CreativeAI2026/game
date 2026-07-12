namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// giveItem ステップの seam。実体は Gameplay(InventoryManager のラッパ)で実装し、
    /// EventPlayer に注入する。Core は Gameplay を参照しないためこの契約を挟む。
    /// </summary>
    public interface IItemGiver
    {
        void Give(string itemKey);
    }
}
