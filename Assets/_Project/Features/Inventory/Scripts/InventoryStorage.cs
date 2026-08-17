using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Inventory data holder. Keep this class close to save data and avoid putting rules here.
    /// </summary>
    public class InventoryStorage
    {
        /// <summary>即時使用食材スロット数(spec §1.2: 即時食材使用UIにセットできる最大3つ)。</summary>
        public const int QuickFoodSlotCount = 3;

        private readonly List<ItemStack> _items = new();

        // 即時食材使用UIが参照する即時使用食材スロット(最大3・順序あり)。
        // 各要素は _items 内の食材スタックへの参照(未セットは null)。ルールは InventoryService 側。
        private readonly ItemStack[] _quickFoodSlots = new ItemStack[QuickFoodSlotCount];

        public List<ItemStack> Items => _items;

        public ItemStack[] QuickFoodSlots => _quickFoodSlots;
    }
}
