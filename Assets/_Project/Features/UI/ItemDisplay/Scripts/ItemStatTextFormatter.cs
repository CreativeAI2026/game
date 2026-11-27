using System.Collections.Generic;
using System.Globalization;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// アイテムの能力値を画面に出す文字の作り方。並び順・名前・数値の書式はここだけで決め、
    /// 詳細欄・パラメータの行・調合画面が同じものを使う。
    /// </summary>
    public static class ItemStatTextFormatter
    {
        /// <summary>装備品の能力値を並べる順番。</summary>
        public static readonly IReadOnlyList<StatType> EquipmentStats = new[]
        {
            StatType.AttackPct,
            StatType.DefensePct,
            StatType.CritRate,
            StatType.CritDamage,
            StatType.MaxHpPct,
        };

        private const string HealLabel = "HP回復";
        private const string PositiveColor = "#A7D8FF";
        private const string NegativeColor = "#FF8A8A";

        /// <summary>能力値の名前(「攻撃」など)。</summary>
        public static string Label(StatType type) =>
            type switch
            {
                StatType.AttackPct => "攻撃",
                StatType.DefensePct => "防御",
                StatType.CritRate => "会心率",
                StatType.CritDamage => "会心ダメージ",
                StatType.MaxHpPct => "最大HP",
                StatType.HealAmount => "回復量",
                _ => type.ToString(),
            };

        /// <summary>数値を小数2桁まで(「12.5」)。</summary>
        public static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>正の値に「+」を付けた割合(「+12.5%」)。</summary>
        public static string SignedPercent(float value) =>
            (value > 0f ? "+" : string.Empty) + Number(value) + "%";

        /// <summary>食材の回復量(「20%」)。食材が無ければ「—」。</summary>
        public static string Recovery(FoodData food) =>
            food == null ? "—" : $"{Number(food.HealFraction * 100f)}%";

        /// <summary>装備に付いている能力値(「+12.5%」)。装備が無ければ「—」。</summary>
        public static string Actual(StatType type, ItemStack stack) =>
            stack?.Data == null ? "—" : $"+{Number(BonusOf(stack).Get(type))}%";

        /// <summary>個体にロールされた能力値の合計。ロールされていなければすべて 0。</summary>
        public static EquipmentBonus BonusOf(ItemStack stack)
        {
            var bonus = new EquipmentBonus();
            if (stack != null && stack.IsInstance)
                bonus.Add(stack.RolledStats);
            return bonus;
        }

        /// <summary>
        /// 所持品1つ分の能力値の表示文(色付き・1行に1つ)。ロールされた個体はロール値を出し、0 の能力は出さない。
        /// </summary>
        public static string BuildStatsText(ItemStack stack)
        {
            var item = stack?.Data;
            if (item == null)
                return string.Empty;

            if (item is FoodData food)
            {
                // 食材はHP即時回復のみ。回復量は最大HPに対する割合(合成前20%/合成後50%)。
                int percent = Mathf.RoundToInt(food.HealFraction * 100f);
                return ColoredLine(HealLabel, percent.ToString(CultureInfo.InvariantCulture));
            }

            var bonus = BonusOf(stack);
            var lines = new List<string>();
            foreach (var type in EquipmentStats)
            {
                float value = bonus.Get(type);
                if (!Mathf.Approximately(value, 0f))
                    lines.Add(ColoredLine(Label(type), Number(value)));
            }
            return string.Join("\n", lines);
        }

        private static string ColoredLine(string label, string number)
        {
            bool negative = number.StartsWith("-", System.StringComparison.Ordinal);
            string color = negative ? NegativeColor : PositiveColor;
            return $"<color={color}>{label} {(negative ? string.Empty : "+")}{number}%</color>";
        }
    }
}
