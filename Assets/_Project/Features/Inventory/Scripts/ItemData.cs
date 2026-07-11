using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public enum ItemCategory
    {
        Weapon,
        Equipment,
        Food,
        Important,
    }

    [CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
    public class ItemData : ScriptableObject
    {
        public Sprite icon; // アイコン
        public int id; // ID
        public string itemName; // アイテム名
        public ItemCategory category; // カテゴリ

        [HideInInspector]
        public string effect; // 互換用: Stats表示はItemStatTextFormatterを使う
        public string description; // 説明

        [SerializeField, Min(1)]
        private int _maxStack = 1;

        public int MaxStack => Mathf.Max(1, _maxStack);
    }

    public static class ItemCategoryExtensions
    {
        public static string ToDisplayName(this ItemCategory category) =>
            category switch
            {
                ItemCategory.Weapon => "武器",
                ItemCategory.Equipment => "装備品",
                ItemCategory.Food => "食材",
                ItemCategory.Important => "重要",
                _ => "",
            };
    }

    public class ItemStack
    {
        public ItemData Data { get; }
        private int _count;

        public int Count
        {
            get => EquipmentInstance != null ? 1 : _count;
            set => _count = EquipmentInstance != null ? 1 : value;
        }

        public EquipmentInstance EquipmentInstance { get; }
        public bool IsEquipped { get; set; }

        public ItemStack(ItemData data, int count = 1)
        {
            Data = data;
            Count = count;
        }

        public ItemStack(EquipmentData data, EquipmentInstance equipmentInstance)
        {
            Data = data;
            EquipmentInstance =
                equipmentInstance
                ?? throw new System.ArgumentNullException(nameof(equipmentInstance));
            Count = 1;
        }
    }

    public static class ItemStatTextFormatter
    {
        private const string HpLabel = "HP";
        private const string AttackLabel = "ATK";
        private const string DefenseLabel = "DEF";
        private const string MoveSpeedLabel = "MOVE";
        private const string AttackSpeedLabel = "ATK SPD";
        private const string CriticalDamageLabel = "CRIT DMG";
        private const string CriticalRateLabel = "CRIT";
        private const string PositiveColor = "#A7D8FF";
        private const string NegativeColor = "#FF8A8A";

        public static string BuildStatsText(ItemData item)
        {
            if (item == null)
                return string.Empty;

            var lines = new List<string>();

            if (item is FoodData food)
            {
                AddFlat(lines, HpLabel, food.healAmount);
                return string.Join("\n", lines);
            }

            if (item is EquipmentData equipment)
            {
                AddPercent(lines, AttackLabel, equipment.attack);
                AddPercent(lines, DefenseLabel, equipment.defense);
                AddPercent(lines, MoveSpeedLabel, equipment.moveSpeed);
                AddPercent(lines, AttackSpeedLabel, equipment.attackSpeed);
                AddPercent(lines, CriticalDamageLabel, equipment.criticalDamage);
                AddPercent(lines, CriticalRateLabel, equipment.criticalRate);
                AddFlat(lines, HpLabel, equipment.maxHP);
            }
            else if (item is WeaponData weapon)
            {
                AddPercent(lines, AttackLabel, weapon.attack);
                AddPercent(lines, DefenseLabel, weapon.defense);
                AddPercent(lines, MoveSpeedLabel, weapon.moveSpeed);
                AddPercent(lines, AttackSpeedLabel, weapon.attackSpeed);
                AddPercent(lines, CriticalDamageLabel, weapon.criticalDamage);
                AddPercent(lines, CriticalRateLabel, weapon.criticalRate);
                AddFlat(lines, HpLabel, weapon.maxHP);
            }

            return string.Join("\n", lines);
        }

        private static void AddFlat(List<string> lines, string label, int value)
        {
            if (value == 0)
                return;

            lines.Add(FormatLine(label, value.ToString(CultureInfo.InvariantCulture), false));
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
