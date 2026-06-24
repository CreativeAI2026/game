using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        private List<ItemStack> _items = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AddTestItems();
        }

        public void AddItem(ItemData data, int count = 1)
        {
            if (data == null)
                return;

            // 同じアイテムがあればスタック
            var existing = _items.Find(s => s.Data == data);
            if (existing != null)
                existing.Count += count;
            else
                _items.Add(new ItemStack(data, count));
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            var stack = _items.Find(s => s.Data == data);
            if (stack == null)
                return;
            stack.Count -= count;
            if (stack.Count <= 0)
                _items.Remove(stack);
        }

        public void SetEquipped(ItemStack stack, bool equipped)
        {
            if (stack != null)
                stack.IsEquipped = equipped;
        }

        public bool IsEquipped(ItemStack stack) => stack?.IsEquipped ?? false;

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return _items.FindAll(i => i.Data.category == category);
        }

        public List<ItemStack> GetAllItems() => new(_items);

        private void AddTestItems()
        {
            if (ItemDB.Instance == null)
                return;

            var item1001 = ItemDB.Instance.GetItemById(1001);
            var item2001 = ItemDB.Instance.GetItemById(2001);
            var item2002 = ItemDB.Instance.GetItemById(2002);
            var item3001 = ItemDB.Instance.GetItemById(3001);
            var item3002 = ItemDB.Instance.GetItemById(3002);
            var item4001 = ItemDB.Instance.GetItemById(4001);

            var allItems = new[] { item1001, item2001, item2002, item3001, item3002, item4001 };

            foreach (var item in allItems)
                if (item != null)
                    AddItem(item);

            for (int i = 0; i < 40; i++)
            {
                var item = allItems[Random.Range(0, allItems.Length)];
                if (item != null)
                    AddItem(item);
            }
        }
    }
}
