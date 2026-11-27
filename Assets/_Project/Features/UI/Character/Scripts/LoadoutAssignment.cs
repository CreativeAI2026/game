using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// キャラクター画面の食材3枠(クイックアイテム)と装備3枠。どの枠を選んでいるか、枠への付け外し・入れ替え、
    /// 枠の表示の更新、開いたときに枠が順に出てくる演出を受け持つ。枠の中身は QuickFoodSlots と EquipmentSlots が持つ。
    /// </summary>
    internal sealed class LoadoutAssignment
    {
        private readonly LoadoutAssignmentSlotView[] _quickSlots;
        private readonly LoadoutAssignmentSlotView[] _equipmentSlots;
        private readonly Dictionary<Transform, Vector3> _restScales = new();
        private Sequence _appearSequence;

        public LoadoutAssignment(
            LoadoutAssignmentSlotView[] quickSlots,
            LoadoutAssignmentSlotView[] equipmentSlots
        )
        {
            _quickSlots = quickSlots;
            _equipmentSlots = equipmentSlots;
            ConfigureDragSources(_quickSlots, ItemSlotDragOrigin.Quick);
            ConfigureDragSources(_equipmentSlots, ItemSlotDragOrigin.Equipment);
        }

        public IReadOnlyList<LoadoutAssignmentSlotView> QuickSlots => _quickSlots;
        public IReadOnlyList<LoadoutAssignmentSlotView> EquipmentSlots => _equipmentSlots;

        /// <summary>選んでいる食材の枠。</summary>
        public int SelectedQuick { get; set; }

        /// <summary>選んでいる装備の枠。</summary>
        public int SelectedEquipment { get; set; }

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

            var quickFood = InventoryManager.CurrentQuickFood;
            if (quickFood == null || !quickFood.Set(SelectedQuick, stack))
                return false;

            RefreshQuick();
            return true;
        }

        /// <summary>選んでいる装備の枠に入れて装備する。前に入っていたものは外す。入れられたら true。</summary>
        public bool AssignEquipment(ItemStack stack)
        {
            if (SelectedEquipment < 0 || SelectedEquipment >= _equipmentSlots.Length)
                return false;

            var equipment = InventoryManager.CurrentEquipment;
            if (equipment == null || !equipment.Set(SelectedEquipment, stack))
                return false;

            RefreshEquipment();
            return true;
        }

        /// <summary>選んでいる食材の枠を空ける。空けられたら true。</summary>
        public bool RemoveQuick()
        {
            if (SelectedQuick < 0 || SelectedQuick >= _quickSlots.Length)
                return false;

            var quickFood = InventoryManager.CurrentQuickFood;
            if (quickFood != null)
                quickFood.Clear(SelectedQuick);
            RefreshQuick();
            return true;
        }

        /// <summary>選んでいる装備の枠を空けて、装備を外す。空けられたら true。</summary>
        public bool RemoveEquipment()
        {
            if (SelectedEquipment < 0 || SelectedEquipment >= _equipmentSlots.Length)
                return false;

            var equipment = InventoryManager.CurrentEquipment;
            if (equipment != null)
                equipment.Clear(SelectedEquipment);
            RefreshEquipment();
            return true;
        }

        /// <summary>食材の枠どうしを入れ替える。入れ替えられたら true。</summary>
        public bool SwapQuick(int from, int to)
        {
            var quickFood = InventoryManager.CurrentQuickFood;
            if (quickFood == null || !quickFood.Swap(from, to))
                return false;

            RefreshQuick();
            return true;
        }

        /// <summary>装備の枠どうしを入れ替える(装備の状態は変わらない)。入れ替えられたら true。</summary>
        public bool SwapEquipment(int from, int to)
        {
            var equipment = InventoryManager.CurrentEquipment;
            if (equipment == null || !equipment.Swap(from, to))
                return false;

            RefreshEquipment();
            return true;
        }

        /// <summary>食材の枠を、持ち物の登録に合わせて描き直す。</summary>
        public void RefreshQuick()
        {
            var quickFood = InventoryManager.CurrentQuickFood;
            IReadOnlyList<ItemStack> assigned = quickFood != null ? quickFood.GetSlots() : null;
            for (int i = 0; i < _quickSlots.Length; i++)
            {
                ItemStack stack = assigned != null && i < assigned.Count ? assigned[i] : null;
                _quickSlots[i].SetStack(stack);
            }
        }

        /// <summary>装備の枠を、装備の登録に合わせて描き直す。</summary>
        public void RefreshEquipment()
        {
            var equipment = InventoryManager.CurrentEquipment;
            IReadOnlyList<ItemStack> assigned = equipment != null ? equipment.GetSlots() : null;
            for (int i = 0; i < _equipmentSlots.Length; i++)
            {
                ItemStack stack = assigned != null && i < assigned.Count ? assigned[i] : null;
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
