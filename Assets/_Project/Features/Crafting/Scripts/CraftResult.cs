namespace CreativeAI.Crafting
{
    /// <summary>
    /// 調合結果。結果アイテムの id(カタログ参照)と、ロール済みステータス。
    /// instanceId の採番やインベントリ反映はセーブ/インベントリ層の責務。
    /// </summary>
    public sealed class CraftResult
    {
        public int ResultItemId { get; }
        public StatVector RolledStats { get; }

        public CraftResult(int resultItemId, StatVector rolledStats)
        {
            ResultItemId = resultItemId;
            RolledStats = rolledStats ?? StatVector.Empty;
        }
    }
}
