using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController
    {
        private readonly HashSet<GameObject> _warnedMissingEquipmentSlots = new();

        private void InitializeSlots()
        {
            UnbindSlots();
            _slots.Clear();

            if (_equipmentSlotsRoot == null)
                return;

            for (int i = 0; i < _equipmentSlotsRoot.childCount; i++)
            {
                var slot = GetEquipmentSlot(_equipmentSlotsRoot.GetChild(i).gameObject);
                if (slot == null)
                    continue;

                slot.Init();
                slot.Clear();
                slot.Clicked += OnEquipmentSlotClicked;
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;

                _slots.Add(slot);
            }
        }

        private EquipmentSlot GetEquipmentSlot(GameObject slotObject)
        {
            var slot = slotObject.GetComponent<EquipmentSlot>();
            if (slot != null)
                return slot;

            if (_warnedMissingEquipmentSlots.Add(slotObject))
            {
                Debug.LogWarning(
                    $"{nameof(EquipmentViewController)}: Slot '{slotObject.name}' に {nameof(EquipmentSlot)} がないため、このスロットをスキップしました。PrefabまたはScene上で追加してください。",
                    slotObject
                );
            }

            return null;
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnEquipmentSlotClicked;
                slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
            }
        }

        private void EquipInitialTestItems()
        {
            var initialItems = InventoryManager
                .Instance?.GetAllItems()
                .Where(stack => stack.Data.category == _inventoryCategory && stack.IsEquipped)
                .Take(Mathf.Min(2, _slots.Count))
                .ToList();

            if (initialItems == null)
                return;

            foreach (var stack in initialItems)
            {
                int slotIndex = initialItems.IndexOf(stack);
                _slots[slotIndex].SetStack(stack);
                InventoryManager.Instance?.SetEquipped(stack, true);
            }
        }

        private void SelectEquipmentSlot(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return;

            var previousSlot = CurrentSlot;
            var selectedSlot = _slots[index];

            previousSlot?.SetSelected(false);

            foreach (var slot in _slots)
                if (slot != null && slot != previousSlot && slot != selectedSlot)
                    slot.SetSelected(false);

            _currentSlotIndex = index;
            selectedSlot.SetSelected(true);

            _selectedInventoryStack = null;
            SyncInventorySelection(CurrentSlot.Stack);

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != selectedSlot
                && previousSlot.Stack == null
                && selectedSlot.Stack == null;
            _detailPanel?.Show(selectedSlot.Item, _emptyLabel, changedBetweenEmptySlots);
        }

        private void OnEquipmentSlotDoubleClicked(EquipmentSlot slot)
        {
            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectEquipmentSlot(slotIndex);
            _selectedInventoryStack = null;
            UnequipCurrentSlot();
        }

        private void OnEquipmentSlotClicked(EquipmentSlot slot)
        {
            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectEquipmentSlot(slotIndex);
        }

        private void SelectNextEmptySlot()
        {
            if (_slots.Count <= 1)
                return;

            int equippedSlotIndex = _currentSlotIndex;
            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (equippedSlotIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Stack != null)
                    continue;

                SelectAndRotateSlot(slotIndex);
                return;
            }
        }

        private void SelectAndRotateSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            SelectEquipmentSlot(slotIndex);
            RotateSlotToTop(slotIndex);
        }

        private int GetTopEquipmentSlotIndex()
        {
            if (_triangleLayout == null)
                return 0;

            int slotIndex = _triangleLayout.GetTopSlotIndex();
            return slotIndex >= 0 && slotIndex < _slots.Count ? slotIndex : 0;
        }

        private void RotateSlotToTop(int slotIndex)
        {
            _equipmentSlotsRoot?.GetComponent<TriangleLayout>()?.RotateSlotToTop(slotIndex);
        }

        private void RefreshSlotLayout()
        {
            _equipmentSlotsRoot?.GetComponent<TriangleLayout>()?.RefreshLayout();
        }

        private void RefreshDetailFromCurrentSlot()
        {
            if (_detailPanel == null || CurrentSlot == null)
                return;

            _detailPanel.Show(CurrentSlot.Item, _emptyLabel);
        }
    }
}
