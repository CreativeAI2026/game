using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController
    {
        private void InitializeSlots()
        {
            UnbindSlots();
            _slots.Clear();

            if (_equipmentSlotsContainer == null)
                return;

            for (int i = 0; i < _equipmentSlotsContainer.childCount; i++)
            {
                var slot = GetOrConvertSlot(_equipmentSlotsContainer.GetChild(i).gameObject);
                if (slot == null)
                    continue;

                int slotIndex = _slots.Count;

                slot.Init();
                slot.Clear();
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;
                if (slot.Button != null)
                {
                    UnityEngine.Events.UnityAction action = () =>
                    {
                        if (IsSlotInputLocked())
                            return;

                        CreativeAI.UI.SlotKeyboardFocus.Claim(this);
                        SelectEquipmentSlot(slotIndex);
                    };
                    _slotButtonActions[slot.Button] = action;
                    slot.Button.onClick.AddListener(action);
                }

                _slots.Add(slot);
            }
        }

        private static EquipmentSlot GetOrConvertSlot(GameObject slotObject)
        {
            var slot = slotObject.GetComponent<EquipmentSlot>();
            if (slot != null)
                return slot;

            var materialSlot = slotObject.GetComponent<MaterialSlot>();
            if (materialSlot == null)
                return null;

            materialSlot.enabled = false;
            return slotObject.AddComponent<EquipmentSlot>();
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
                if (
                    slot.Button != null
                    && _slotButtonActions.TryGetValue(slot.Button, out var action)
                )
                {
                    slot.Button.onClick.RemoveListener(action);
                    _slotButtonActions.Remove(slot.Button);
                }
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
            _currentSlotIndex = index;

            for (int i = 0; i < _slots.Count; i++)
            {
                bool selected = i == index;
                _slots[i].SetFrameColor(selected ? SlotFrameSelected : SlotFrameNormal);
                _slots[i].SetSelected(selected);
            }

            _selectedInventoryStack = null;
            SyncInventorySelection(CurrentSlot.Stack);

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != CurrentSlot
                && previousSlot.Stack == null
                && CurrentSlot.Stack == null;
            _detailPanel?.Show(CurrentSlot.Item, _emptyLabel, changedBetweenEmptySlots);
        }

        private void OnEquipmentSlotDoubleClicked(EquipmentSlot slot)
        {
            if (IsSlotInputLocked())
                return;

            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectEquipmentSlot(slotIndex);
            _selectedInventoryStack = null;
            UnequipCurrentSlot();
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
            _equipmentSlotsContainer?.GetComponent<TriangleLayout>()?.RotateSlotToTop(slotIndex);
        }

        private void RefreshSlotLayout()
        {
            _equipmentSlotsContainer?.GetComponent<TriangleLayout>()?.RefreshLayout();
        }

        private void RefreshDetailFromCurrentSlot()
        {
            if (_detailPanel == null || CurrentSlot == null)
                return;

            _detailPanel.Show(CurrentSlot.Item, _emptyLabel);
        }
    }
}
