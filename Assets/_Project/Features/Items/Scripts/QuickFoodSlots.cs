using System;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 即時食材の枠。キー(1/2/3)や食材バーからすぐ食べられるようにセットした食材(最大 <see cref="SlotCount"/> 枠・順序あり)。
    /// 所持品(<see cref="InventoryManager.QuickFood"/>)の一部で、所持品にある食材だけを入れられる。
    /// 束が所持品から消えるときは、InventoryManager が同じ操作の中でここから外す。
    /// </summary>
    public sealed class QuickFoodSlots
    {
        /// <summary>枠の数。</summary>
        public const int SlotCount = 3;

        /// <summary>枠の中身が変わったとき。食材バー・キャラ画面の食材枠が購読する。</summary>
        public event Action Changed;

        private readonly ItemStack[] _slots = new ItemStack[SlotCount];
        private readonly InventoryManager _inventory;

        internal QuickFoodSlots(InventoryManager inventory) => _inventory = inventory;

        /// <summary>今の枠の中身(要素は食材の束 or null)。読み取り専用の写し。</summary>
        public IReadOnlyList<ItemStack> GetSlots() => (ItemStack[])_slots.Clone();

        /// <summary>stack がどれかの枠に入っているか(調合の素材から外す判定に使う)。</summary>
        public bool Contains(ItemStack stack) => stack != null && Array.IndexOf(_slots, stack) >= 0;

        /// <summary>
        /// 枠 slot に食材の束を入れる。所持品にある食材(FoodData)だけ受け付ける。
        /// 同じ束が別の枠にあれば移す(重複して入れない)。範囲外や食材以外は false。
        /// </summary>
        public bool Set(int slot, ItemStack stack)
        {
            if (slot < 0 || slot >= _slots.Length)
                return false;
            if (stack == null || stack.Data is not FoodData || !_inventory.ContainsStack(stack))
                return false;

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == stack)
                    _slots[i] = null;
            _slots[slot] = stack;
            Changed?.Invoke();
            return true;
        }

        /// <summary>枠 slot を空にする。既に空なら何もしない。</summary>
        public void Clear(int slot)
        {
            if (slot < 0 || slot >= _slots.Length || _slots[slot] == null)
                return;
            _slots[slot] = null;
            Changed?.Invoke();
        }

        /// <summary>2つの枠の中身を、1回の変更通知で入れ替える。</summary>
        public bool Swap(int firstSlot, int secondSlot)
        {
            if (
                firstSlot < 0
                || firstSlot >= _slots.Length
                || secondSlot < 0
                || secondSlot >= _slots.Length
                || firstSlot == secondSlot
            )
                return false;
            (_slots[firstSlot], _slots[secondSlot]) = (_slots[secondSlot], _slots[firstSlot]);
            Changed?.Invoke();
            return true;
        }

        // 以下は InventoryManager だけが呼ぶ。所持品と枠をまとめて変えてから、最後に通知するため、ここでは通知しない。

        // 所持品から消える束を枠から外す(使い切った・素材にした食材)。外したら true。
        internal bool Release(ItemStack stack)
        {
            bool changed = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == stack)
                {
                    _slots[i] = null;
                    changed = true;
                }
            }
            return changed;
        }

        // ロードで枠をまるごと差し替える。所持品に無い束・食材でない束は入れない。
        internal void Restore(IReadOnlyList<ItemStack> slots)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var stack = slots != null && i < slots.Count ? slots[i] : null;
                bool valid = stack?.Data is FoodData && _inventory.ContainsStack(stack);
                _slots[i] = valid ? stack : null;
            }
        }

        internal void NotifyChanged() => Changed?.Invoke();
    }
}
