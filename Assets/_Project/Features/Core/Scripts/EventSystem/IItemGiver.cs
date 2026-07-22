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

    /// <summary>
    /// 実行時に有効な IItemGiver を Core 側へ登録する seam。Gameplay の InventoryManager が
    /// Awake で自身を登録し、EventPlayer は Inspector 未配線時のフォールバックとしてここを見る
    /// (_progress / _gameMode が .Instance に頼るのと同じ思想)。InventoryManager は実行時生成の
    /// 常駐シングルトンでシーンから drag 配線できず、かつ Core は Gameplay を参照できないため、
    /// 具象ではなくこの契約経由で受け取る。
    /// </summary>
    public static class ItemGiverService
    {
        public static IItemGiver Current { get; set; }
    }
}
