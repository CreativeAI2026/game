using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.DevTools
{
    /// <summary>
    /// 開発シーン用のテスト品。所持品が空なら基礎素材を積み、装備品を先頭から数個装備しておく。
    /// 本番のタイトルからの開始では使わない(所持品はまっさらで始まる)。
    /// </summary>
    public static class DevTestItems
    {
        private const int MinCount = 5;
        private const int MaxCountExclusive = 16;
        private const int EquippedCount = 2;

        /// <summary>所持品が空のときだけテスト品を積んで装備する。既に何か持っていれば何もしない。</summary>
        public static void Seed(InventoryManager inventory)
        {
            var catalog = ItemCatalog.Instance;
            if (inventory == null || catalog == null || inventory.GetAllItems().Count > 0)
                return;

            foreach (var item in catalog.Items.Where(item => item != null && item.IsBaseMaterial))
            {
                int count = item.MaxStack > 1 ? Random.Range(MinCount, MaxCountExclusive) : 1;
                inventory.AddAcquired(item, count);
            }

            var equipment = inventory.Equipment;
            var gear = inventory.GetItemsByCategory(ItemCategory.Equipment);
            for (int i = 0; i < EquippedCount && i < gear.Count; i++)
                equipment.Set(i, gear[i]);
        }
    }
}
