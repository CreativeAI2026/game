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

        /// <summary>
        /// 戦闘食材スロット(最大3)の内容が変わったときに発火。
        /// 即時食材使用UI / CharacterUI 戦闘食材タブが購読して表示を更新する。
        /// </summary>
        public event Action BattleFoodChanged;

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

        public ItemStack AddInstance(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            if (data == null)
                return null;

            var stack = new ItemStack(data, rolledStats);
            _storage.Items.Add(stack);
            InventoryChanged?.Invoke();
            return stack;
        }

        public void Clear()
        {
            bool hadBattleFood = ClearAllBattleFoodSlots();

            if (_storage.Items.Count == 0)
            {
                if (hadBattleFood)
                    BattleFoodChanged?.Invoke();
                return;
            }

            _storage.Items.Clear();
            InventoryChanged?.Invoke();
            if (hadBattleFood)
                BattleFoodChanged?.Invoke();
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
            bool battleFoodChanged = false;
            if (stack.Count <= 0)
            {
                _storage.Items.Remove(stack);
                battleFoodChanged = ClearBattleFoodReferencing(stack);
            }

            InventoryChanged?.Invoke();
            if (battleFoodChanged)
                BattleFoodChanged?.Invoke();
            return true;
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return;

            int remaining = count;
            bool removedAny = false;
            bool battleFoodChanged = false;
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
                {
                    _storage.Items.RemoveAt(i);
                    battleFoodChanged |= ClearBattleFoodReferencing(stack);
                }
            }

            if (removedAny)
                InventoryChanged?.Invoke();
            if (battleFoodChanged)
                BattleFoodChanged?.Invoke();
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

        // --- 戦闘食材スロット(最大3・順序あり)。即時食材使用UIにセットする食材の選択状態 ---

        /// <summary>戦闘食材スロットの現在の内容(要素は食材スタック or null)。読み取り専用のスナップショット。</summary>
        public IReadOnlyList<ItemStack> GetBattleFoodSlots() =>
            (ItemStack[])_storage.BattleFoodSlots.Clone();

        /// <summary>
        /// スロット slot に食材スタックをセットする。食材(FoodData)かつ在庫にあるスタックのみ受け付ける。
        /// 同じスタックが別スロットにあれば移動(重複セットを防ぐ)。範囲外や非食材は false。
        /// </summary>
        public bool SetBattleFood(int slot, ItemStack stack)
        {
            var slots = _storage.BattleFoodSlots;
            if (slot < 0 || slot >= slots.Length)
                return false;
            if (stack == null || stack.Data is not FoodData || !_storage.Items.Contains(stack))
                return false;

            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == stack)
                    slots[i] = null;

            slots[slot] = stack;
            BattleFoodChanged?.Invoke();
            return true;
        }

        /// <summary>スロット slot を空にする。既に空なら何もしない。</summary>
        public void ClearBattleFood(int slot)
        {
            var slots = _storage.BattleFoodSlots;
            if (slot < 0 || slot >= slots.Length || slots[slot] == null)
                return;

            slots[slot] = null;
            BattleFoodChanged?.Invoke();
        }

        /// <summary>指定スタックを参照している戦闘食材スロットを空にする(在庫から消えたときの後始末)。発火は呼び出し側。</summary>
        private bool ClearBattleFoodReferencing(ItemStack stack)
        {
            var slots = _storage.BattleFoodSlots;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == stack)
                {
                    slots[i] = null;
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>全スロットを空にする(Clear 用)。発火は呼び出し側。</summary>
        private bool ClearAllBattleFoodSlots()
        {
            var slots = _storage.BattleFoodSlots;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i] = null;
                    changed = true;
                }
            }
            return changed;
        }

        private IEnumerable<ItemStack> GetStacksWithRoom(ItemData data, int maxStack)
        {
            return _storage.Items.Where(stack =>
                stack.Data == data && !stack.IsInstance && stack.Count < maxStack
            );
        }
    }
}
