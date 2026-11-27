using System;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// いま、そのイベントを発火できる条件が揃っているか(進行度・フラグ・所持品・1回きりの見終え)。
    /// 自動で発火する <see cref="EventTrigger"/> と、キーで発火するインタラクタが同じ判定を使う。
    /// </summary>
    public static class EventAvailability
    {
        /// <summary>条件を満たすか。常駐の <see cref="ProgressManager"/> が無ければ false。</summary>
        public static bool IsMet(EventDefinition ev) => IsMet(ev, ProgressManager.Instance);

        /// <summary>条件を満たすか。<paramref name="progress"/> が null なら false(テストは Instance を介さず渡せる)。</summary>
        public static bool IsMet(EventDefinition ev, ProgressManager progress)
        {
            if (ev == null || progress == null)
                return false;

            // hasItem 条件用の所持判定。所持品が居なければ所持なし扱い。
            var inventory = InventoryManager.Instance;
            Func<string, bool> hasItem = inventory != null ? inventory.HasImportantItem : null;
            return ev.ConditionsMet(progress.Progress, progress.GetFlag, hasItem);
        }
    }
}
