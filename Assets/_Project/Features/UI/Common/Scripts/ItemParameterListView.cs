using System;
using System.Globalization;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    public sealed class ItemParameterListView : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public TMP_Text label;
            public TMP_Text value;
        }

        [SerializeField]
        private Row[] _equipmentRows;

        [SerializeField]
        private Row _recoveryRow;
        private static readonly string[] Labels =
        {
            "攻撃",
            "防御",
            "会心率",
            "会心ダメージ",
            "最大HP",
        };

        public void SetItem(ItemStack stack)
        {
            bool equipment = stack?.Data?.category == ItemCategory.Equipment;
            var bonus = new EquipmentBonus();
            if (equipment && stack.IsInstance)
                bonus.Add(stack.RolledStats);
            float[] values =
            {
                bonus.attackPct,
                bonus.defensePct,
                bonus.criticalChance,
                bonus.criticalDamage,
                bonus.maxHpPct,
            };
            if (_equipmentRows != null)
                for (int i = 0; i < _equipmentRows.Length && i < Labels.Length; i++)
                    SetRow(_equipmentRows[i], equipment, Labels[i], values[i]);
            var food = stack?.Data as FoodData;
            SetRow(
                _recoveryRow,
                food != null,
                "回復量",
                food != null ? food.HealFraction * 100 : 0
            );
            gameObject.SetActive(equipment || food != null);
        }

        private static void SetRow(Row row, bool visible, string label, float value)
        {
            if (row?.root == null)
                return;
            row.root.SetActive(visible);
            row.label.text = label;
            row.value.text =
                (value > 0 ? "+" : "") + value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }
    }
}
