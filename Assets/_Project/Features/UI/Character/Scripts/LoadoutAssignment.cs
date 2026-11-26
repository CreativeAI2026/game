using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// キャラクター画面の食材3枠(クイックアイテム)と装備3枠。どの枠を選んでいるか、枠への付け外し・入れ替え、
    /// 枠の表示の更新、開いたときに枠が順に出てくる演出を受け持つ。
    /// 装備はどの枠に入れたかを持たないので、装備中のものをこの画面の中で枠に割り振って覚えておく。
    /// </summary>
    internal sealed class LoadoutAssignment
    {
        private const int EquipmentSlotCount = 3;

        private readonly LoadoutAssignmentSlotView[] _quickSlots;
        private readonly LoadoutAssignmentSlotView[] _equipmentSlots;
        private readonly ItemStack[] _equipment = new ItemStack[EquipmentSlotCount];
        private readonly Dictionary<Transform, Vector3> _restScales = new();
        private readonly Func<InventoryManager> _inventoryManager;
        private Sequence _appearSequence;

        /// <param name="inventoryManager">今使う InventoryManager を返す(無ければ null)</param>
        public LoadoutAssignment(
            LoadoutAssignmentSlotView[] quickSlots,
            LoadoutAssignmentSlotView[] equipmentSlots,
            Func<InventoryManager> inventoryManager
        )
        {
            _quickSlots = quickSlots;
            _equipmentSlots = equipmentSlots;
            _inventoryManager = inventoryManager;
            ConfigureDragSources(_quickSlots, ItemSlotDragOrigin.Quick);
            ConfigureDragSources(_equipmentSlots, ItemSlotDragOrigin.Equipment);
        }

        public IReadOnlyList<LoadoutAssignmentSlotView> QuickSlots => _quickSlots;
        public IReadOnlyList<LoadoutAssignmentSlotView> EquipmentSlots => _equipmentSlots;

        /// <summary>選んでいる食材の枠。</summary>
        public int SelectedQuick { get; set; }

        /// <summary>選んでいる装備の枠。</summary>
        public int SelectedEquipment { get; set; }

        /// <summary>この画面から装備を付け外ししている最中か(その間の装備変更の通知は無視する)。</summary>
        public bool IsChangingEquipment { get; private set; }

        /// <summary>枠を1つずつ、0 から元の大きさへ弾ませて出す。</summary>
        public void PlayAppearance(float duration, float stagger, object target)
        {
            StopAppearance();
            _appearSequence = DOTween.Sequence().SetUpdate(true).SetTarget(target);
            int index = 0;
            foreach (var slot in _quickSlots.Concat(_equipmentSlots))
            {
                if (slot == null)
                    continue;
                var transform = slot.transform;
                if (!_restScales.TryGetValue(transform, out var restScale))
                {
                    restScale = transform.localScale;
                    _restScales.Add(transform, restScale);
                }
                transform.localScale = Vector3.zero;
                _appearSequence.Insert(
                    index * stagger,
                    transform.DOScale(restScale, duration).SetEase(Ease.OutBack)
                );
                index++;
            }
        }

        public void StopAppearance()
        {
            _appearSequence?.Kill();
            _appearSequence = null;
            foreach (var entry in _restScales)
                if (entry.Key != null)
                    entry.Key.localScale = entry.Value;
        }

        /// <summary>選んだ枠に印を付ける。category の側の選んでいる枠だけに付け、反対側は外す。</summary>
        public void Highlight(ItemCategory category)
        {
            bool equipment = category == ItemCategory.Equipment;
            SetSelected(_quickSlots, equipment ? -1 : SelectedQuick);
            SetSelected(_equipmentSlots, equipment ? SelectedEquipment : -1);
        }

        /// <summary>どの枠にも印を付けない。</summary>
        public void ClearHighlight()
        {
            SetSelected(_quickSlots, -1);
            SetSelected(_equipmentSlots, -1);
        }

        /// <summary>アイテムを入れる先として、空いている枠(無ければ今の枠)を選んで印を付ける。</summary>
        public void SelectTargetFor(ItemCategory category)
        {
            if (category == ItemCategory.Food)
                SelectedQuick = FindPreferredSlot(_quickSlots, SelectedQuick);
            else if (category == ItemCategory.Equipment)
                SelectedEquipment = FindPreferredSlot(_equipmentSlots, SelectedEquipment);
            else
                return;
            Highlight(category);
        }

        /// <summary>入れられる側の枠だけを明るくする。</summary>
        public void SetAvailable(bool quick, bool equipment)
        {
            foreach (var slot in _quickSlots)
                if (slot != null)
                    slot.SetAvailable(quick);
            foreach (var slot in _equipmentSlots)
                if (slot != null)
                    slot.SetAvailable(equipment);
        }

        /// <summary>ドラッグ中の入れ替え後の見せ方を消す。</summary>
        public void ClearPreviews()
        {
            foreach (var slot in _quickSlots.Concat(_equipmentSlots))
                if (slot != null)
                    slot.ClearPreview();
        }

        /// <summary>選んでいる食材の枠に入れる。入れられたら true。</summary>
        public bool AssignQuick(ItemStack stack)
        {
            if (SelectedQuick < 0 || SelectedQuick >= _quickSlots.Length)
                return false;

            var manager = _inventoryManager();
            if (manager == null || !manager.SetQuickFood(SelectedQuick, stack))
                return false;

            RefreshQuick();
            return true;
        }

        /// <summary>選んでいる装備の枠に入れて装備する。前に入っていたものは外す。入れられたら true。</summary>
        public bool AssignEquipment(ItemStack stack)
        {
            if (
                SelectedEquipment < 0
                || SelectedEquipment >= _equipment.Length
                || stack.Data.category != ItemCategory.Equipment
            )
                return false;

            var manager = _inventoryManager();
            if (manager == null)
                return false;

            IsChangingEquipment = true;
            try
            {
                for (int i = 0; i < _equipment.Length; i++)
                {
                    if (i != SelectedEquipment && _equipment[i] == stack)
                        _equipment[i] = null;
                }

                ItemStack previous = _equipment[SelectedEquipment];
                if (previous != null && previous != stack)
                    manager.SetEquipped(previous, false);

                manager.SetEquipped(stack, true);
                if (stack.IsEquipped)
                    _equipment[SelectedEquipment] = stack;
            }
            finally
            {
                IsChangingEquipment = false;
            }

            SyncEquipment();
            RefreshEquipment();
            return true;
        }

        /// <summary>選んでいる食材の枠を空ける。空けられたら true。</summary>
        public bool RemoveQuick()
        {
            if (SelectedQuick < 0 || SelectedQuick >= _quickSlots.Length)
                return false;

            var manager = _inventoryManager();
            if (manager != null)
                manager.ClearQuickFood(SelectedQuick);
            RefreshQuick();
            return true;
        }

        /// <summary>選んでいる装備の枠を空けて、装備を外す。空けられたら true。</summary>
        public bool RemoveEquipment()
        {
            if (SelectedEquipment < 0 || SelectedEquipment >= _equipment.Length)
                return false;

            ItemStack stack = _equipment[SelectedEquipment];
            IsChangingEquipment = true;
            try
            {
                var manager = _inventoryManager();
                if (manager != null)
                    manager.SetEquipped(stack, false);
                _equipment[SelectedEquipment] = null;
            }
            finally
            {
                IsChangingEquipment = false;
            }

            RefreshEquipment();
            return true;
        }

        /// <summary>食材の枠どうしを入れ替える。入れ替えられたら true。</summary>
        public bool SwapQuick(int from, int to)
        {
            var manager = _inventoryManager();
            if (manager == null || !manager.SwapQuickFood(from, to))
                return false;

            RefreshQuick();
            return true;
        }

        /// <summary>装備の枠どうしを入れ替える(装備の状態は変わらない)。入れ替えられたら true。</summary>
        public bool SwapEquipment(int from, int to)
        {
            if (from < 0 || from >= _equipment.Length || from == to)
                return false;

            (_equipment[from], _equipment[to]) = (_equipment[to], _equipment[from]);
            RefreshEquipment();
            return true;
        }

        /// <summary>食材の枠を、持ち物の登録に合わせて描き直す。</summary>
        public void RefreshQuick()
        {
            var manager = _inventoryManager();
            IReadOnlyList<ItemStack> assigned =
                manager != null ? manager.GetQuickFoodSlots() : null;
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                ItemStack stack = assigned != null && i < assigned.Count ? assigned[i] : null;
                _quickSlots[i].SetStack(stack);
            }
        }

        /// <summary>
        /// 覚えている装備の割り振りを、実際の装備に合わせる。外れたもの・無くなったものは枠から消し、
        /// 枠に無い装備中のものは空いている枠に入れる。
        /// </summary>
        public void SyncEquipment()
        {
            var manager = _inventoryManager();
            List<ItemStack> allItems = manager != null ? manager.GetAllItems() : null;
            if (allItems == null)
                return;

            for (int i = 0; i < _equipment.Length; i++)
            {
                ItemStack stack = _equipment[i];
                if (stack != null && (!stack.IsEquipped || !allItems.Contains(stack)))
                    _equipment[i] = null;
            }

            foreach (
                ItemStack stack in allItems.Where(item =>
                    item?.Data != null
                    && item.Data.category == ItemCategory.Equipment
                    && item.IsEquipped
                )
            )
            {
                if (_equipment.Contains(stack))
                    continue;

                int empty = Array.IndexOf(_equipment, null);
                if (empty < 0)
                    break;
                _equipment[empty] = stack;
            }
        }

        /// <summary>装備の枠を、覚えている割り振りに合わせて描き直す。</summary>
        public void RefreshEquipment()
        {
            for (int i = 0; i < _equipmentSlots.Length; i++)
            {
                ItemStack stack = i < _equipment.Length ? _equipment[i] : null;
                _equipmentSlots[i].SetStack(stack);
            }
        }

        private static int FindPreferredSlot(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            int selectedIndex
        )
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Stack == null)
                    return i;

            return slots.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, slots.Count - 1);
        }

        private static void SetSelected(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            int selectedIndex
        )
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetSelected(i == selectedIndex);
        }

        private static void ConfigureDragSources(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            ItemSlotDragOrigin origin
        )
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].ConfigureDragSource(origin, i);
        }
    }
}
