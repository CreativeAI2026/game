using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory
    {
        private Tween _scrollTween;

        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            _releaseSelectionOnOutsideClick = release;

            foreach (var slot in _visibleSlots)
                slot.SetReleaseSelectionOnOutsideClick(release);
        }

        private void RefreshSlots(List<ItemStack> items, ScrollRefreshMode scrollMode)
        {
            KillScrollTween();
            var scrollRect = GetScrollRect();
            float previousVerticalPosition = scrollRect?.verticalNormalizedPosition ?? 1f;
            float previousHorizontalPosition = scrollRect?.horizontalNormalizedPosition ?? 0f;
            ClearSlots();

            if (_slotsRoot == null || _slotPrefab == null || items == null)
            {
                _currentSelectedSlot = null;
                return;
            }

            int index = 0;
            foreach (var stack in items)
            {
                if (stack == null)
                    continue;

                CreateSlot(stack, index);
                index++;
            }

            RestoreSelectionAfterRefresh();
            RebuildLayoutAndApplyScroll(
                scrollRect,
                scrollMode,
                previousVerticalPosition,
                previousHorizontalPosition
            );
        }

        private ItemSlot CreateSlot(ItemStack stack, int index)
        {
            var slot = GetSlotFromPool();
            slot.gameObject.SetActive(true);
            slot.transform.SetAsLastSibling();
            slot.transform.DOKill();
            slot.SetReleaseSelectionOnOutsideClick(_releaseSelectionOnOutsideClick);
            slot.SetItem(stack);
            slot.SetCraftAssigned(IsCraftAssigned(stack));
            _visibleSlots.Add(slot);

            slot.transform.localScale = Vector3.zero;
            slot.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetDelay(0.05f * index);

            return slot;
        }

        private ItemSlot GetSlotFromPool()
        {
            InitializeSlotPool();

            foreach (var slot in _pooledSlots)
            {
                if (slot != null && !_visibleSlots.Contains(slot) && !slot.gameObject.activeSelf)
                    return slot;
            }

            var createdSlot = Instantiate(_slotPrefab, _slotsRoot, false);
            _pooledSlots.Add(createdSlot);
            return createdSlot;
        }

        private void InitializeSlotPool()
        {
            if (_slotPoolInitialized || _slotsRoot == null)
                return;

            _slotPoolInitialized = true;
            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slot = _slotsRoot.GetChild(i).GetComponent<ItemSlot>();
                if (slot == null || _pooledSlots.Contains(slot))
                    continue;

                _pooledSlots.Add(slot);
                ReturnSlotToPool(slot);
            }
        }

        private void RestoreSelectionAfterRefresh()
        {
            if (_visibleSlots.Count <= 0)
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
                SelectSlot(_visibleSlots[0]);
            else
                _currentSelectedSlot = null;
        }

        private ScrollRect GetScrollRect()
        {
            return _slotsRoot is RectTransform contentRect
                ? contentRect.GetComponentInParent<ScrollRect>()
                : null;
        }

        private void RebuildLayoutAndApplyScroll(
            ScrollRect scrollRect,
            ScrollRefreshMode scrollMode,
            float previousVerticalPosition,
            float previousHorizontalPosition
        )
        {
            if (_slotsRoot is not RectTransform contentRect)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (scrollRect == null)
                return;

            if (scrollMode == ScrollRefreshMode.KeepPosition)
            {
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(previousVerticalPosition);
                scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(previousHorizontalPosition);
                return;
            }

            _scrollTween = DOTween
                .To(
                    () => scrollRect.verticalNormalizedPosition,
                    value => scrollRect.verticalNormalizedPosition = value,
                    1f,
                    0.3f
                )
                .SetEase(Ease.OutQuint)
                .SetTarget(scrollRect)
                .OnKill(() => _scrollTween = null);
        }

        private void KillScrollTween()
        {
            _scrollTween?.Kill();
            _scrollTween = null;
        }

        private void ClearSlots()
        {
            InitializeSlotPool();

            foreach (var slot in _visibleSlots)
                ReturnSlotToPool(slot);

            _visibleSlots.Clear();
        }

        private static void ReturnSlotToPool(ItemSlot slot)
        {
            if (slot == null)
                return;

            slot.transform.DOKill();
            slot.Deselect();
            slot.SetCraftAssigned(false);
            slot.SetItem(null);
            slot.transform.localScale = Vector3.one;
            slot.gameObject.SetActive(false);
        }

        private ItemSlot FindVisibleSlot(ItemStack stack)
        {
            if (stack == null)
                return null;

            foreach (var slot in _visibleSlots)
            {
                if (slot != null && slot.gameObject.activeSelf && slot.Stack == stack)
                    return slot;
            }

            return null;
        }
    }
}
