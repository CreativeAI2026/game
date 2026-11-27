using System;
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

        public void SetItem(ItemStack stack)
        {
            bool equipment =
                stack != null
                && stack.Data != null
                && stack.Data.category == ItemCategory.Equipment;
            var bonus = equipment ? ItemStatTextFormatter.BonusOf(stack) : new EquipmentBonus();
            var stats = ItemStatTextFormatter.EquipmentStats;
            if (_equipmentRows != null)
                for (int i = 0; i < _equipmentRows.Length && i < stats.Count; i++)
                    SetRow(
                        _equipmentRows[i],
                        equipment,
                        ItemStatTextFormatter.Label(stats[i]),
                        bonus.Get(stats[i])
                    );
            var food = stack?.Data as FoodData;
            SetRow(
                _recoveryRow,
                food != null,
                ItemStatTextFormatter.Label(StatType.HealAmount),
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
            row.value.text = ItemStatTextFormatter.SignedPercent(value);
        }
    }
}
