using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// キャラクター画面の枠へのドラッグ&amp;ドロップの見せ方。ドラッグ中は入れられる側の枠を明るくし、
    /// 枠の上に来たら入れ替え後の様子を見せ、入れられない枠に落としたら理由を出す。
    /// ドラッグの開始・終了と、落とした後の付け外しは画面の側が受ける。
    /// </summary>
    internal sealed class LoadoutDragDrop
    {
        private readonly LoadoutAssignment _slots;
        private readonly LoadoutDropErrorFeedback _errorFeedback;

        /// <param name="errorFeedback">入れられない理由を出す部品(無ければ出さない)</param>
        public LoadoutDragDrop(LoadoutAssignment slots, LoadoutDropErrorFeedback errorFeedback)
        {
            _slots = slots;
            _errorFeedback = errorFeedback;
        }

        public void Bind()
        {
            foreach (var slot in AllSlots())
            {
                slot.DropRejected += OnDropRejected;
                slot.DragEntered += OnDragEntered;
                slot.DragExited += OnDragExited;
            }
        }

        public void Unbind()
        {
            foreach (var slot in AllSlots())
            {
                if (slot == null)
                    continue;
                slot.DropRejected -= OnDropRejected;
                slot.DragEntered -= OnDragEntered;
                slot.DragExited -= OnDragExited;
            }
        }

        private IEnumerable<LoadoutAssignmentSlotView> AllSlots()
        {
            foreach (var slot in _slots.QuickSlots)
                yield return slot;
            foreach (var slot in _slots.EquipmentSlots)
                yield return slot;
        }

        private void OnDropRejected(LoadoutAssignmentSlotView target, ItemSlotDragPayload payload)
        {
            string message =
                Contains(_slots.QuickSlots, target)
                    ? "クイックアイテムには回復アイテムのみ登録できます"
                : Contains(_slots.EquipmentSlots, target) ? "装備スロットには装備品のみ登録できます"
                : "このスロットには登録できません";

            if (_errorFeedback != null)
                _errorFeedback.Show(message, target.transform as RectTransform);
        }

        // 落とす先の枠には持っているアイテムを、入れ替えになるなら元の枠には落とす先のアイテムを見せる。
        private void OnDragEntered(LoadoutAssignmentSlotView target, ItemSlotDragPayload payload)
        {
            _slots.ClearPreviews();
            target.ShowPreview(payload.Stack);

            IReadOnlyList<LoadoutAssignmentSlotView> sourceSlots = payload.Origin switch
            {
                ItemSlotDragOrigin.Quick => _slots.QuickSlots,
                ItemSlotDragOrigin.Equipment => _slots.EquipmentSlots,
                _ => null,
            };
            if (
                sourceSlots == null
                || payload.SlotIndex < 0
                || payload.SlotIndex >= sourceSlots.Count
            )
                return;

            LoadoutAssignmentSlotView source = sourceSlots[payload.SlotIndex];
            if (source != target && target.Stack != null)
                source.ShowPreview(target.Stack);
        }

        private void OnDragExited(LoadoutAssignmentSlotView _) => _slots.ClearPreviews();

        private static bool Contains(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            LoadoutAssignmentSlotView target
        )
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == target)
                    return true;
            return false;
        }
    }
}
