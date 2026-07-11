using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Central place for inventory mutations and queries.
    /// Item effects and crafting rules should live outside this service.
    /// </summary>
    public class InventoryService
    {
        private readonly InventoryStorage _storage;

        public InventoryService(InventoryStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public event Action InventoryChanged;

        public void AddItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return;

            int remaining = count;
            int maxStack = data.MaxStack;

            foreach (var stack in GetStacksWithRoom(data, maxStack))
            {
                int addCount = Math.Min(remaining, maxStack - stack.Count);
                stack.Count += addCount;
                remaining -= addCount;

                if (remaining <= 0)
                    break;
            }

            while (remaining > 0)
            {
                int stackCount = Math.Min(remaining, maxStack);
                _storage.Items.Add(new ItemStack(data, stackCount));
                remaining -= stackCount;
            }

            InventoryChanged?.Invoke();
        }

        public void AddEquipmentItem(EquipmentData data, EquipmentInstance instance)
        {
            if (data == null || instance == null)
                return;

            _storage.Items.Add(new ItemStack(data, instance));
            InventoryChanged?.Invoke();
        }

        public bool ConsumeItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0 || GetItemCount(data) < count)
                return false;

            RemoveItem(data, count);
            return true;
        }

        public bool ConsumeFromStack(ItemStack stack, int count = 1)
        {
            if (stack == null || count <= 0 || stack.Count < count)
                return false;

            if (!_storage.Items.Contains(stack))
                return false;

            stack.Count -= count;
            if (stack.Count <= 0)
                _storage.Items.Remove(stack);

            InventoryChanged?.Invoke();
            return true;
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return;

            int remaining = count;
            bool removedAny = false;
            for (int i = _storage.Items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _storage.Items[i];
                if (stack.Data != data)
                    continue;

                int removeCount = Math.Min(stack.Count, remaining);
                stack.Count -= removeCount;
                remaining -= removeCount;
                removedAny = true;

                if (stack.Count <= 0)
                    _storage.Items.RemoveAt(i);
            }

            if (removedAny)
                InventoryChanged?.Invoke();
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return data != null && count > 0 && GetItemCount(data) >= count;
        }

        public int GetItemCount(ItemData data)
        {
            if (data == null)
                return 0;

            return _storage.Items.Where(stack => stack.Data == data).Sum(stack => stack.Count);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return _storage.Items.FindAll(stack =>
                stack.Data != null && stack.Data.category == category
            );
        }

        public bool ContainsStack(ItemStack stack)
        {
            return stack != null && _storage.Items.Contains(stack);
        }

        public List<ItemStack> GetAllItems() => new(_storage.Items);

        private IEnumerable<ItemStack> GetStacksWithRoom(ItemData data, int maxStack)
        {
            return _storage.Items.Where(stack =>
                stack.Data == data && stack.EquipmentInstance == null && stack.Count < maxStack
            );
        }
    }
}
