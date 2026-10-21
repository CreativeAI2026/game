using System.Globalization;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    public static class CraftParameterTextFormatter
    {
        public static string Recovery(FoodData food) =>
            food == null ? "—" : $"{Number(food.HealFraction * 100f)}%";

        public static string Actual(StatType type, ItemStack stack) =>
            stack?.Data == null ? "—" : $"+{Number(RolledStat.ToVector(stack.RolledStats)[type])}%";

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
            return candidates.Length == 1 ? $"+{Number(budget)}%" : $"0～{Number(budget)}%";
        }

        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
