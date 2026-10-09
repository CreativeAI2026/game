using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public enum ItemCategory
    {
        // 明示値で既存アセットの serialized category(1/2/3)を保つ。
        // 武器はインベントリ在庫の対象外(WeaponManager 管理)なので、この3カテゴリに含めない。
        Equipment = 1,
        Food = 2,
        Important = 3,
    }

    [CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
    public class ItemData : ScriptableObject
    {
        public Sprite icon; // アイコン
        public int id; // ID
        public string key; // events.json の giveItem/itemKey が参照する文字列キー(任意。大事なもの等で使用)
        public string itemName; // アイテム名
        public ItemCategory category; // カテゴリ
        public string description; // 説明

        [SerializeField, Min(1)]
        private int _maxStack = 1;

        /// <summary>1スタックに積める上限。既定は1(装備品・大事なもの=積まない)。カテゴリで積めるものは派生でルール化する。</summary>
        public virtual int MaxStack => Mathf.Max(1, _maxStack);
    }

    public static class ItemCategoryExtensions
    {
        public static string ToDisplayName(this ItemCategory category) =>
            category switch
            {
                ItemCategory.Equipment => "装備品",
                ItemCategory.Food => "食材",
                ItemCategory.Important => "大事なもの",
                _ => "",
            };
    }

    /// <summary>
    /// 調合・拾得でロールされた個体ステータス1つ。stat は <see cref="StatType"/> 名(例: "AttackPct")で、
    /// 読み取りは大文字小文字を無視する。
    /// </summary>
    [System.Serializable]
    public sealed class RolledStat
    {
        public string stat;
        public float value;

        public RolledStat() { }

        public RolledStat(string stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }

        public static IReadOnlyList<RolledStat> FromVector(StatVector rolled)
        {
            var list = new List<RolledStat>(rolled.Count);
            foreach (var type in rolled.Types)
                list.Add(new RolledStat(type.ToString(), rolled[type]));
            return list;
        }
    }

    public class ItemStack
    {
        public ItemData Data { get; }
        private int _count;

        public int Count
        {
            get => IsInstance ? 1 : _count;
            set => _count = IsInstance ? 1 : value;
        }

        public bool IsEquipped { get; set; }

        /// <summary>
        /// 調合でロールされた個体ステータス(装備品/武器の個体差)。
        /// null/空 = 未ロールのスタック品(数量でまとめる)。値あり = 個体(マージ不可)。
        /// </summary>
        public IReadOnlyList<RolledStat> RolledStats { get; }

        /// <summary>ロール済み個体か(true ならマージ・スタックしない)。</summary>
        public bool IsInstance => RolledStats != null && RolledStats.Count > 0;

        public ItemStack(ItemData data, int count = 1)
        {
            Data = data;
            Count = count;
        }

        /// <summary>ロール済み個体を1つ作る（数量は常に1・マージしない）。</summary>
        public ItemStack(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            Data = data;
            RolledStats = rolledStats;
            Count = 1;
        }
    }

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
            else if (item is EquipmentData equipment)
                AddFixedStats(
                    ref bonus,
                    equipment.attack,
                    equipment.defense,
                    equipment.criticalDamage,
                    equipment.criticalRate,
                    equipment.maxHP
                );
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
