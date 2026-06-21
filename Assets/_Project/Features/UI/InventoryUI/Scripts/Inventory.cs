using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class Inventory : MonoBehaviour
    {
        [SerializeField]
        private bool _selectFirstSlotOnRefresh = true;

        [Header("Tab")]
        [SerializeField]
        private TabGroup _tabGroup;

        [SerializeField]
        private List<ItemCategory> _categories;

        [Header("Slots")]
        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private ItemSlot _slotPrefab;

        [Header("Detail")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        public System.Action<ItemStack> OnSlotClicked;

        private List<ItemCategory> _activeCategories = new();
        private bool _navigationDisabled = false;
        private ItemSlot _currentSelectedSlot;
        private ItemSlot _equippedSlot;
        private ItemStack _selectedStack;

        public void SetSelectFirstSlotOnRefresh(bool selectFirst) =>
            _selectFirstSlotOnRefresh = selectFirst;

        private void Awake()
        {
            _tabGroup = GetComponentInChildren<TabGroup>();
            _tabGroup.OnTabSelected += OnTabSelected;
        }

        private void Start()
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_tabGroup.IsEnabled(i))
                    _activeCategories.Add(_categories[i]);
            }

            OnTabSelected(0);
        }

        private void OnDestroy()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabSelected -= OnTabSelected;
        }

        private void OnTabSelected(int index)
        {
            if (index < 0 || index >= _activeCategories.Count)
                return;
            var items = InventoryManager.Instance?.GetItemsByCategory(_activeCategories[index]);
            RefreshSlots(items);
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

                var slot = Instantiate(_slotPrefab, _slotsRoot, false);
                slot.SetItem(stack);

                var rt = slot.GetComponent<RectTransform>();
                rt.localScale = Vector3.zero;
                rt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetDelay(0.05f * index);

                index++;
            }

            if (_slotsRoot.childCount > 0)
            {
                var firstSlot = _slotsRoot.GetChild(0).GetComponent<ItemSlot>();
                var selectedSlot = FindVisibleSlot(_selectedStack);
                if (selectedSlot != null)
                {
                    _currentSelectedSlot = selectedSlot;
                    selectedSlot.Select();
                }
                else if (_selectFirstSlotOnRefresh)
                    SelectSlot(firstSlot); // ロックあり
                else
                {
                    _currentSelectedSlot = null;
                }
            }
            else
            {
                _currentSelectedSlot = null;
            }

            if (_slotsRoot is RectTransform contentRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

                var scroll = contentRect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    DOTween
                        .To(
                            () => scroll.verticalNormalizedPosition,
                            x => scroll.verticalNormalizedPosition = x,
                            1f,
                            0.3f
                        )
                        .SetEase(Ease.OutQuint);
                }
            }
        }

        public void SelectSlot(ItemSlot slot)
        {
            if (slot == null)
                return;

            slot.Select();
            _currentSelectedSlot = slot;
            _selectedStack = slot.Stack;
            _detailPanel?.Show(slot.Item);

            if (!_navigationDisabled && EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                _navigationDisabled = true;
            }
        }

        public void SelectSlotByClick(ItemSlot slot)
        {
            SelectSlot(slot);
            OnSlotClicked?.Invoke(slot.Stack); // クリック時だけ発火
        }

        public void HighlightEquippedItem(ItemStack stack)
        {
            if (_equippedSlot != null)
                _equippedSlot.SetEquipped(false);

            _equippedSlot = _currentSelectedSlot;
            if (_equippedSlot != null)
                _equippedSlot.SetEquipped(true);
        }

        public void UpdateItemEquippedState(ItemStack stack, bool isEquipped, bool keepSelected)
        {
            if (stack == null || _slotsRoot == null)
                return;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slot = _slotsRoot.GetChild(i).GetComponent<ItemSlot>();
                if (slot == null || slot.Stack != stack)
                    continue;

                slot.SetEquipped(isEquipped);

                if (isEquipped)
                    _equippedSlot = slot;
                else if (_equippedSlot == slot)
                    _equippedSlot = null;

                if (keepSelected)
                {
                    _selectedStack = stack;
                    _currentSelectedSlot = slot;
                    slot.Select();
                }

                return;
            }
        }

        public void SelectItem(ItemStack stack)
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _selectedStack = stack;
            _currentSelectedSlot = FindVisibleSlot(stack);
            _currentSelectedSlot?.Select();
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

        private void ClearSlots()
        {
            if (_slotsRoot == null)
                return;
            for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(_slotsRoot.GetChild(i).gameObject);
        }

        public void ResetToTop()
        {
            if (_slotsRoot is RectTransform contentRect)
            {
                var scroll = contentRect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1f;
            }

            if (_slotsRoot != null && _slotsRoot.childCount > 0)
            {
                var firstSlot = _slotsRoot.GetChild(0).GetComponent<ItemSlot>();
                SelectSlot(firstSlot);
            }
        }

        public void RefreshCurrentTab()
        {
            OnTabSelected(_tabGroup.CurrentIndex);
        }

        public void ResetToFirstTab()
        {
            _tabGroup?.ResetToFirstTab();
        }
    }
}
