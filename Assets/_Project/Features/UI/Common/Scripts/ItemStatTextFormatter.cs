using System.Collections.Generic;
using System.Globalization;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    public static class ItemStatTextFormatter
    {
        private const string HpLabel = "最大HP";
        private const string HealLabel = "HP回復";
        private const string AttackLabel = "攻撃";
        private const string DefenseLabel = "防御";
        private const string CriticalDamageLabel = "会心ダメージ";
        private const string CriticalRateLabel = "会心率";
        private const string PositiveColor = "#A7D8FF";
        private const string NegativeColor = "#FF8A8A";

        /// <summary>
        /// 所持品1つ分の能力値の表示文。調合・拾得でロールされた個体はロール値を、
        /// それ以外はアイテムの固定値を出す(装備時の補正計算と同じ値)。
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
                return FormatLine(HealLabel, percent.ToString(CultureInfo.InvariantCulture), true);
            }

            var bonus = new EquipmentBonus();
            if (stack.IsInstance)
                bonus.Add(stack.RolledStats);
            else if (item is WeaponData weapon)
                AddFixedStats(
                    ref bonus,
                    weapon.attack,
                    weapon.defense,
                    weapon.criticalDamage,
                    weapon.criticalRate,
                    weapon.maxHP
                );

            var lines = new List<string>();
            AddPercent(lines, AttackLabel, bonus.attackPct);
            AddPercent(lines, DefenseLabel, bonus.defensePct);
            AddPercent(lines, CriticalDamageLabel, bonus.criticalDamage);
            AddPercent(lines, CriticalRateLabel, bonus.criticalChance);
            AddPercent(lines, HpLabel, bonus.maxHpPct);
            return string.Join("\n", lines);
        }

        private static void AddFixedStats(
            ref EquipmentBonus bonus,
            float attack,
            float defense,
            float criticalDamage,
            float criticalRate,
            float maxHp
        )
        {
            bonus.attackPct += attack;
            bonus.defensePct += defense;
            bonus.criticalDamage += criticalDamage;
            bonus.criticalChance += criticalRate;
            bonus.maxHpPct += maxHp;
        }

        private static void AddPercent(List<string> lines, string label, float value)
        {
            if (Mathf.Approximately(value, 0f))
                return;

            lines.Add(FormatLine(label, FormatNumber(value), true));
        }

        private static string FormatLine(string label, string formattedValue, bool isPercent)
        {
            bool isNegative = formattedValue.StartsWith("-", System.StringComparison.Ordinal);
            string sign = isNegative ? string.Empty : "+";
            string unit = isPercent ? "%" : string.Empty;
            string color = isNegative ? NegativeColor : PositiveColor;
            return $"<color={color}>{label} {sign}{formattedValue}{unit}</color>";
        }

        private static string FormatNumber(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
