using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>調合画面に出す、できあがりの見込みの文字を作る。書式は <see cref="ItemStatTextFormatter"/> に合わせる。</summary>
    public static class CraftParameterTextFormatter
    {
        /// <summary>
        /// 2つの装備を調合したときに付きうる値の幅。必ず付くなら「+N%」、付くかもしれないなら「0～N%」、
        /// 付かないなら「+0%」。素材がそろっていなければ「—」。
        /// </summary>
        public static string Preview(StatType type, ItemStack first, ItemStack second)
        {
            if (first?.Data == null || second?.Data == null)
                return "—";
            var a = RolledStat.ToVector(first.RolledStats);
            var b = RolledStat.ToVector(second.RolledStats);
            var p = StatRollParameters.Default;
            var candidates = a
                .Types.Union(b.Types)
                .OrderByDescending(t =>
                    (a[t] + b[t]) * (a[t] > 0f && b[t] > 0f ? 1d + p.Synergy : 1d)
                )
                .ThenBy(t => t)
                .Take(p.MaxStatCount)
                .ToArray();
            if (!candidates.Contains(type))
                return "+0%";
            float budget = (float)CraftingService.ComputePowerBudget(a.Power, b.Power, p);
            if (p.Caps.TryGetValue(type, out float cap))
                budget = Mathf.Min(budget, cap);
            string number = ItemStatTextFormatter.Number(budget);
            return candidates.Length == 1 ? $"+{number}%" : $"0～{number}%";
        }
    }
}
