using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory
    {
        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            _releaseSelectionOnOutsideClick = release;

            if (_slotsRoot == null)
                return;

            foreach (var slot in _slotsRoot.GetComponentsInChildren<ItemSlot>(true))
                slot.SetReleaseSelectionOnOutsideClick(release);
        }

        private void RefreshSlots(List<ItemStack> items)
        {
            ClearSlots();

            if (_slotsRoot == null || _slotPrefab == null || items == null)
                return;

            int index = 0;
            foreach (var stack in items)
            {
                if (stack == null)
                    continue;

                var slot = CreateSlot(stack, index);
                slot.SetCraftAssigned(IsCraftAssigned(stack));
                index++;
            }

            RestoreSelectionAfterRefresh();
            RebuildLayoutAndScrollToTop();
        }

        private ItemSlot CreateSlot(ItemStack stack, int index)
        {
            var slot = Instantiate(_slotPrefab, _slotsRoot, false);
            slot.SetReleaseSelectionOnOutsideClick(_releaseSelectionOnOutsideClick);
            slot.SetItem(stack);

            var rectTransform = slot.GetComponent<RectTransform>();
            rectTransform.localScale = Vector3.zero;
            rectTransform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetDelay(0.05f * index);

            return slot;
        }

        private void RestoreSelectionAfterRefresh()
        {
            if (_slotsRoot.childCount <= 0)
            {
                _currentSelectedSlot = null;
                return;
            }

            var selectedSlot = FindVisibleSlot(_selectedStack);
            if (selectedSlot != null)
            {
                _currentSelectedSlot = selectedSlot;
                selectedSlot.Select();
                return;
            }

            if (_selectFirstSlotOnRefresh)
                SelectSlot(_slotsRoot.GetChild(0).GetComponent<ItemSlot>());
            else
                _currentSelectedSlot = null;
        }

        private void RebuildLayoutAndScrollToTop()
        {
            if (_slotsRoot is not RectTransform contentRect)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            var scroll = contentRect.GetComponentInParent<ScrollRect>();
            if (scroll == null)
                return;

            DOTween
                .To(
                    () => scroll.verticalNormalizedPosition,
                    value => scroll.verticalNormalizedPosition = value,
                    1f,
                    0.3f
                )
                .SetEase(Ease.OutQuint);
        }

        private void ClearSlots()
        {
            if (_slotsRoot == null)
                return;

            for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _slotsRoot.GetChild(i);
                child.DOKill();
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private ItemSlot FindVisibleSlot(ItemStack stack)
        {
            if (stack == null || _slotsRoot == null)
                return null;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slot = _slotsRoot.GetChild(i).GetComponent<ItemSlot>();
                if (slot != null && slot.Stack == stack)
                    return slot;
            }

            return null;
        }
    }
}
