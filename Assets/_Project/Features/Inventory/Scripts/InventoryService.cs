using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 所持品の中身(アイテムの束 + 即時使用食材スロット)と、その追加・消費・検索・食材の使用。
    /// 調合のルールは CraftingService 側に置く。
    /// </summary>
    public class InventoryService
    {
        /// <summary>即時使用食材スロット数(即時食材使用UIにセットできる最大3つ)。</summary>
        public const int QuickFoodSlotCount = 3;

        private readonly List<ItemStack> _items = new();

        // 即時食材使用UIが参照する即時使用食材スロット(順序あり)。各要素は _items 内の食材スタックへの参照(未セットは null)。
        private readonly ItemStack[] _quickFoodSlots = new ItemStack[QuickFoodSlotCount];

        public event Action InventoryChanged;

        /// <summary>
        /// 即時使用食材スロット(最大3)の内容が変わったときに発火。
        /// 即時食材使用UI / CharacterUI 即時使用食材タブが購読して表示を更新する。
        /// </summary>
        public event Action QuickFoodChanged;

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
                _items.Add(new ItemStack(data, stackCount));
                remaining -= stackCount;
            }

            InventoryChanged?.Invoke();
        }

        public ItemStack AddInstance(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            if (data == null)
                return null;

            var stack = new ItemStack(data, rolledStats);
            _items.Add(stack);
            InventoryChanged?.Invoke();
            return stack;
        }

        public void Clear()
        {
            bool hadQuickFood = ClearAllQuickFoodSlots();

            if (_items.Count == 0)
            {
                if (hadQuickFood)
                    QuickFoodChanged?.Invoke();
                return;
            }

            _items.Clear();
            InventoryChanged?.Invoke();
            if (hadQuickFood)
                QuickFoodChanged?.Invoke();
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

            if (!_items.Contains(stack))
                return false;

            stack.Count -= count;
            bool quickFoodChanged = false;
            if (stack.Count <= 0)
            {
                _items.Remove(stack);
                quickFoodChanged = ClearQuickFoodReferencing(stack);
            }

            InventoryChanged?.Invoke();
            if (quickFoodChanged)
                QuickFoodChanged?.Invoke();
            return true;
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return;

            int remaining = count;
            bool removedAny = false;
            bool quickFoodChanged = false;
            for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _items[i];
                if (stack.Data != data)
                    continue;

                int removeCount = Math.Min(stack.Count, remaining);
                stack.Count -= removeCount;
                remaining -= removeCount;
                removedAny = true;

                if (stack.Count <= 0)
                {
                    _items.RemoveAt(i);
                    quickFoodChanged |= ClearQuickFoodReferencing(stack);
                }
            }

            if (removedAny)
                InventoryChanged?.Invoke();
            if (quickFoodChanged)
                QuickFoodChanged?.Invoke();
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return data != null && count > 0 && GetItemCount(data) >= count;
        }

        public int GetItemCount(ItemData data)
        {
            if (data == null)
                return 0;

            return _items.Where(stack => stack.Data == data).Sum(stack => stack.Count);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return _items.FindAll(stack => stack.Data != null && stack.Data.category == category);
        }

        public bool ContainsStack(ItemStack stack)
        {
            return stack != null && _items.Contains(stack);
        }

        public List<ItemStack> GetAllItems() => new(_items);

        // --- 即時使用食材スロット(最大3・順序あり)。即時食材使用UIにセットする食材の選択状態 ---

        /// <summary>即時使用食材スロットの現在の内容(要素は食材スタック or null)。読み取り専用のスナップショット。</summary>
        public IReadOnlyList<ItemStack> GetQuickFoodSlots() => (ItemStack[])_quickFoodSlots.Clone();

        /// <summary>stack が即時使用食材スロットにセットされているか(調合の素材から除外する判定に使う)。</summary>
        public bool IsInQuickFood(ItemStack stack)
        {
            if (stack == null)
                return false;
            var slots = _quickFoodSlots;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == stack)
                    return true;
            return false;
        }

        /// <summary>
        /// スロット slot に食材スタックをセットする。食材(FoodData)かつ在庫にあるスタックのみ受け付ける。
        /// 同じスタックが別スロットにあれば移動(重複セットを防ぐ)。範囲外や非食材は false。
        /// </summary>
        public bool SetQuickFood(int slot, ItemStack stack)
        {
            var slots = _quickFoodSlots;
            if (slot < 0 || slot >= slots.Length)
                return false;
            if (stack == null || stack.Data is not FoodData || !_items.Contains(stack))
                return false;

            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == stack)
                    slots[i] = null;

            slots[slot] = stack;
            QuickFoodChanged?.Invoke();
            return true;
        }

        /// <summary>スロット slot を空にする。既に空なら何もしない。</summary>
        public void ClearQuickFood(int slot)
        {
            var slots = _quickFoodSlots;
            if (slot < 0 || slot >= slots.Length || slots[slot] == null)
                return;

            slots[slot] = null;
            QuickFoodChanged?.Invoke();
        }

        /// <summary>指定スタックを参照している即時使用食材スロットを空にする(在庫から消えたときの後始末)。発火は呼び出し側。</summary>
        private bool ClearQuickFoodReferencing(ItemStack stack)
        {
            var slots = _quickFoodSlots;
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
        private bool ClearAllQuickFoodSlots()
        {
            var slots = _quickFoodSlots;
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
            return _items.Where(stack =>
                stack.Data == data && !stack.IsInstance && stack.Count < maxStack
            );
        }

        /// <summary>
        /// 食材を1つ使う(HP即時回復)。回復量は最大HPに対する固定割合(合成前20%/合成後50%)。
        /// 回復先の playerStatus が無ければ消費もしない(使用と効果は不可分)。調合の素材消費からは呼ばない。
        /// </summary>
        public bool TryUseFood(ItemStack stack, PlayerStatus playerStatus)
        {
            if (
                stack == null
                || !ContainsStack(stack)
                || stack.Count <= 0
                || stack.Data is not FoodData food
            )
                return false;

            if (playerStatus == null)
            {
                Debug.LogWarning(
                    "[InventoryService] PlayerStatus が見つからないため食材の使用を中止しました(在庫は消費しません)。"
                );
                return false;
            }

            playerStatus.Heal(playerStatus.CurrentMaxHp * food.HealFraction);
            return ConsumeFromStack(stack, 1);
        }
    }
}
