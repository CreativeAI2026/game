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
        private List<GameObject> _slots = new();

        private readonly List<MaterialSlot> _resolvedSlots = new();

        public event Action<int> SlotClicked;
        public event Action<int> SlotDoubleClicked;

        public int SlotCount => _resolvedSlots.Count;
        public bool HasRequiredReferences =>
            _resolvedSlots.Count == _slots.Count
            && _resolvedSlots.Count > 0
            && _resolvedSlots.All(slot => slot != null)
            && _resolvedSlots.Distinct().Count() == _resolvedSlots.Count;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_slots.Count != 0)
                return;

            _slots = GetComponentsInChildren<MaterialSlot>(true)
                .OrderBy(slot => slot.transform.GetSiblingIndex())
                .Select(slot => slot.gameObject)
                .ToList();
        }
#endif

        private void OnEnable()
        {
            ResolveConfiguredSlots();
            UnsubscribeSlots();
            SubscribeSlots();
            NormalizeVisualState();
        }

        private void OnDisable()
        {
            UnsubscribeSlots();
        }

        public bool IsValidIndex(int index) => index >= 0 && index < _resolvedSlots.Count;

        public void SetSelectedIndex(int index)
        {
            for (int i = 0; i < _resolvedSlots.Count; i++)
                _resolvedSlots[i]?.SetSelected(i == index);
        }

        public void SetMaterial(int index, ItemStack stack, bool animated)
        {
            if (!IsValidIndex(index))
                return;

            if (animated)
                _resolvedSlots[index].SetMaterialAnimated(stack);
            else
                _resolvedSlots[index].SetMaterial(stack);
        }

        public void ClearMaterial(int index, bool animated, Action onCleared = null)
        {
            if (!IsValidIndex(index))
                return;

            if (animated)
            {
                _resolvedSlots[index].ClearMaterialAnimated(onCleared);
                return;
            }

            _resolvedSlots[index].Clear();
            onCleared?.Invoke();
        }

        public void ResetAll()
        {
            foreach (var slot in _resolvedSlots)
            {
                if (slot == null)
                    continue;

                slot.Clear();
                slot.SetSelected(false);
            }
        }

        private void NormalizeVisualState()
        {
            foreach (var slot in _resolvedSlots)
                slot?.NormalizeVisualState();
        }

        private void SubscribeSlots()
        {
            foreach (var slot in _resolvedSlots)
            {
                if (slot == null)
                    continue;

                slot.Clicked += OnSlotClicked;
                slot.DoubleClicked += OnSlotDoubleClicked;
            }
        }

        private void UnsubscribeSlots()
        {
            foreach (var slot in _resolvedSlots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnSlotClicked;
                slot.DoubleClicked -= OnSlotDoubleClicked;
            }
        }

        private void OnSlotClicked(MaterialSlot slot)
        {
            int index = _resolvedSlots.IndexOf(slot);
            if (index >= 0)
                SlotClicked?.Invoke(index);
        }

        private void OnSlotDoubleClicked(MaterialSlot slot)
        {
            int index = _resolvedSlots.IndexOf(slot);
            if (index >= 0)
                SlotDoubleClicked?.Invoke(index);
        }

        private void ResolveConfiguredSlots()
        {
            _resolvedSlots.Clear();
            foreach (var slotObject in _slots)
            {
                if (slotObject != null && slotObject.TryGetComponent(out MaterialSlot slot))
                    _resolvedSlots.Add(slot);
                else
                    _resolvedSlots.Add(null);
            }
        }
    }
}
