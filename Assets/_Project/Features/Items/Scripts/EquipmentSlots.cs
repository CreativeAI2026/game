using System;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 装備の枠(<see cref="SlotCount"/> 枠・順序あり)。所持品(<see cref="InventoryManager.Equipment"/>)の一部で、
    /// 所持品にある装備品だけを入れられる。束が所持品から消えるときは、InventoryManager が同じ操作の中でここから外す。
    /// 装備中かどうかは枠に入っているか(<see cref="Contains"/>)で決まり、束の側には持たない。
    /// 能力値の合計(<see cref="GetBonus"/>)は PlayerStatus が読む。武器は所持品の外で WeaponManager が持つ。
    /// </summary>
    public sealed class EquipmentSlots
    {
        /// <summary>枠の数(同時に装備できる数)。</summary>
        public const int SlotCount = 3;

        /// <summary>装備が変わったとき(着脱・入れ替え・装備中の物が所持品から消えたとき)。</summary>
        public event Action Changed;

        private readonly ItemStack[] _slots = new ItemStack[SlotCount];
        private readonly InventoryManager _inventory;

        internal EquipmentSlots(InventoryManager inventory) => _inventory = inventory;

        /// <summary>今の枠の中身(要素は装備品の束 or null)。読み取り専用の写し。</summary>
        public IReadOnlyList<ItemStack> GetSlots() => (ItemStack[])_slots.Clone();

        /// <summary>stack を装備しているか(どれかの枠に入っているか)。</summary>
        public bool Contains(ItemStack stack) => stack != null && Array.IndexOf(_slots, stack) >= 0;

        /// <summary>
        /// 枠 slot に装備品の束を入れて装備する。前に入っていたものは外れる。所持品にある装備品だけ受け付ける。
        /// 同じ束が別の枠にあれば移す(重複して入れない)。範囲外や装備品以外は false。
        /// </summary>
        public bool Set(int slot, ItemStack stack)
        {
            if (!IsValidSlot(slot))
                return false;
            if (
                stack?.Data == null
                || stack.Data.category != ItemCategory.Equipment
                || !_inventory.ContainsStack(stack)
            )
                return false;

            int current = Array.IndexOf(_slots, stack);
            if (current >= 0)
                _slots[current] = null;
            _slots[slot] = stack;
            Changed?.Invoke();
            return true;
        }

        /// <summary>枠 slot を空けて、入っていた装備を外す。既に空なら何もしない。</summary>
        public void Clear(int slot)
        {
            if (!IsValidSlot(slot) || _slots[slot] == null)
                return;
            _slots[slot] = null;
            Changed?.Invoke();
        }

        /// <summary>2つの枠の中身を入れ替える(装備しているものは変わらない)。</summary>
        public bool Swap(int firstSlot, int secondSlot)
        {
            if (!IsValidSlot(firstSlot) || !IsValidSlot(secondSlot) || firstSlot == secondSlot)
                return false;
            (_slots[firstSlot], _slots[secondSlot]) = (_slots[secondSlot], _slots[firstSlot]);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 装備中の装備品の補正合計(武器は WeaponManager 側で別合算)。
        /// 装備品の能力値は個体ごとのロール値(RolledStats)だけ。ロールされていない装備品は補正なし。
        /// </summary>
        public EquipmentBonus GetBonus()
        {
            var bonus = new EquipmentBonus();
            foreach (var stack in _slots)
            {
                if (stack != null)
                    bonus.Add(stack.RolledStats);
            }
            return bonus;
        }

        // 以下は InventoryManager だけが呼ぶ。所持品と枠をまとめて変えてから、最後に通知するため、ここでは通知しない。

        // 所持品から消える束を枠から外す。外したら true。
        internal bool Release(ItemStack stack)
        {
            int slot = Array.IndexOf(_slots, stack);
            if (slot < 0)
                return false;
            _slots[slot] = null;
            return true;
        }

        // ロードで枠をまるごと差し替える。所持品に無い束・装備品でない束は入れない。
        internal void Restore(IReadOnlyList<ItemStack> slots)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var stack = slots != null && i < slots.Count ? slots[i] : null;
                bool valid =
                    stack?.Data != null
                    && stack.Data.category == ItemCategory.Equipment
                    && _inventory.ContainsStack(stack);
                _slots[i] = valid ? stack : null;
            }
        }

        internal void NotifyChanged() => Changed?.Invoke();

        private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;
    }
}
