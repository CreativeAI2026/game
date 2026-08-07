using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class FreeCraftMaterialSlotsView : MonoBehaviour
    {
        [SerializeField]
        private List<MaterialSlot> _slots = new();

        public event Action<int> SlotClicked;
        public event Action<int> SlotDoubleClicked;

        public int SlotCount => _slots.Count;
        public bool HasRequiredReferences =>
            _slots.Count > 0
            && _slots.All(slot => slot != null)
            && _slots.Distinct().Count() == _slots.Count;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_slots.Count != 0)
                return;

            _slots = GetComponentsInChildren<MaterialSlot>(true)
                .OrderBy(slot => slot.transform.GetSiblingIndex())
                .ToList();
        }
#endif

        private void OnEnable()
        {
            UnsubscribeSlots();
            SubscribeSlots();
            NormalizeVisualState();
        }

        private void OnDisable()
        {
            UnsubscribeSlots();
        }

        public bool IsValidIndex(int index) => index >= 0 && index < _slots.Count;

        public void SetSelectedIndex(int index)
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i]?.SetSelected(i == index);
        }

        public void SetMaterial(int index, ItemStack stack, bool animated)
        {
            if (!IsValidIndex(index))
                return;

            if (animated)
                _slots[index].SetMaterialAnimated(stack);
            else
                _slots[index].SetMaterial(stack);
        }

        public void ClearMaterial(int index, bool animated, Action onCleared = null)
        {
            if (!IsValidIndex(index))
                return;

            if (animated)
            {
                _slots[index].ClearMaterialAnimated(onCleared);
                return;
            }

            _slots[index].Clear();
            onCleared?.Invoke();
        }

        public void ResetAll()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clear();
                slot.SetSelected(false);
            }
        }

        private void NormalizeVisualState()
        {
            foreach (var slot in _slots)
                slot?.NormalizeVisualState();
        }

        private void SubscribeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked += OnSlotClicked;
                slot.DoubleClicked += OnSlotDoubleClicked;
            }
        }

        private void UnsubscribeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnSlotClicked;
                slot.DoubleClicked -= OnSlotDoubleClicked;
            }
        }

        private void OnSlotClicked(MaterialSlot slot)
        {
            int index = _slots.IndexOf(slot);
            if (index >= 0)
                SlotClicked?.Invoke(index);
        }

        private void OnSlotDoubleClicked(MaterialSlot slot)
        {
            int index = _slots.IndexOf(slot);
            if (index >= 0)
                SlotDoubleClicked?.Invoke(index);
        }
    }
}
