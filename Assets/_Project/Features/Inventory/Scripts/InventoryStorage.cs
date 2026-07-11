using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Inventory data holder. Keep this class close to save data and avoid putting rules here.
    /// </summary>
    public class InventoryStorage
    {
        private readonly List<ItemStack> _items = new();

        public List<ItemStack> Items => _items;
    }
}
