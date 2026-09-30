using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.InventoryUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "Inventory"
    )]
    public class InventoryView : MonoBehaviour
    {
        public enum ScrollRefreshMode
        {
            KeepPosition,
            ScrollToTop,
        }

        [SerializeField]
        private bool _selectFirstSlotOnRefresh = true;

        [SerializeField]
        private bool _showItemCounts = true;

        private bool _releaseSelectionOnOutsideClick = true;

        [Header("Tab")]
        [SerializeField]
        private TabGroup _tabGroup;

        [Header("Slots")]
        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private GameObject _slotPrefab;

        [Header("Detail")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private bool _showOnlyBaseItems = false;

        public event System.Action<ItemStack> OnSlotClicked;
        public event System.Action<ItemStack> OnSlotDoubleClicked;
        public event System.Action<ItemStack> OnSlotSubmitted;
        public event System.Action<TabDefinition, int, ScrollRefreshMode> DisplayRefreshRequested;
        public event System.Action<ItemCategory, ScrollRefreshMode> ItemsRequested;

        private bool _navigationDisabled;
        private bool _interactionEnabled = true;
        private bool _previousSendNavigationEvents;
        private bool _started;
        private Coroutine _resetRoutine;
        private ItemSlot _currentSelectedSlot;
        private ItemStack _selectedStack;
        private readonly List<ItemSlot> _visibleSlots = new();
        private readonly List<ItemSlot> _pooledSlots = new();
        private readonly HashSet<ItemData> _craftAssignedItems = new();
        private readonly HashSet<ItemStack> _craftAssignedStacks = new();
        private bool _slotPoolInitialized;
        private bool _hasWarnedMissingDetailPanel;
        private bool _hasWarnedMissingItemsProvider;
        private bool _hasWarnedMissingDisplayProvider;

        public void SetSelectFirstSlotOnRefresh(bool selectFirst) =>
            _selectFirstSlotOnRefresh = selectFirst;

        public void SetShowItemCounts(bool show)
        {
            _showItemCounts = show;
            foreach (var slot in _pooledSlots)
                slot?.SetShowCount(show);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            if (!enabled)
                CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        private void Awake()
        {
            WarnMissingReferencesOnce();

            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected += OnTabDefinitionSelected;
        }

        private void Start()
        {
            _started = true;
            RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            StopResetRoutine();
            _resetRoutine = StartCoroutine(ResetViewNextFrame());
        }

        private void OnDisable()
        {
            StopResetRoutine();
            KillScrollTween();
            RestoreNavigation();
        }

        private void OnDestroy()
        {
            KillScrollTween();
            RestoreNavigation();

            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected -= OnTabDefinitionSelected;
        }

        public void RefreshCurrentTab() => RefreshCurrentTab(ScrollRefreshMode.KeepPosition);

        public void SetItems(List<ItemStack> items) =>
            SetItems(items, ScrollRefreshMode.KeepPosition);

        public void SetItems(List<ItemStack> items, ScrollRefreshMode scrollMode) =>
            RefreshSlots(FilterVisibleItems(items), scrollMode);

        private void RefreshCurrentTab(ScrollRefreshMode scrollMode)
        {
            int tabIndex = _tabGroup != null ? Mathf.Max(0, _tabGroup.CurrentIndex) : 0;
            TabDefinition definition = _tabGroup?.CurrentDefinition;
            if (definition == null && _tabGroup != null)
                definition = _tabGroup.GetDefinitionForButtonIndex(tabIndex);

            if (TryRequestDisplayRefresh(definition, tabIndex, scrollMode))
                return;

            WarnMissingDisplayProviderOnce();
            SetItems(null, scrollMode);
        }

        public void ResetToFirstTab()
        {
            if (_tabGroup != null)
                _tabGroup.ResetToFirstTab();
            else
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        private IEnumerator ResetViewNextFrame()
        {
            yield return null;

            ResetViewState();
            _resetRoutine = null;
        }

        private void StopResetRoutine()
        {
            if (_resetRoutine == null)
                return;

            StopCoroutine(_resetRoutine);
            _resetRoutine = null;
        }

        private void OnTabDefinitionSelected(int index, TabDefinition definition)
        {
            if (!TryRequestDisplayRefresh(definition, index, ScrollRefreshMode.ScrollToTop))
            {
                WarnMissingDisplayProviderOnce();
                SetItems(null, ScrollRefreshMode.ScrollToTop);
            }
        }

        private bool TryRequestDisplayRefresh(
            TabDefinition definition,
            int tabIndex,
            ScrollRefreshMode scrollMode
        )
        {
            if (DisplayRefreshRequested == null)
                return false;

            DisplayRefreshRequested.Invoke(definition, tabIndex, scrollMode);
            return true;
        }

        public void RequestItems(ItemCategory category, ScrollRefreshMode scrollMode)
        {
            if (ItemsRequested != null)
            {
                ItemsRequested.Invoke(category, scrollMode);
                return;
            }

            if (!_hasWarnedMissingItemsProvider)
            {
                Debug.LogWarning(
                    $"{nameof(InventoryView)} '{name}' has no ItemsRequested subscriber for category '{category}'. "
                        + $"Connect an Inventory data provider controller to this {nameof(InventoryView)}.",
                    this
                );
                _hasWarnedMissingItemsProvider = true;
            }

            SetItems(null, scrollMode);
        }

        private void WarnMissingDisplayProviderOnce()
        {
            if (_hasWarnedMissingDisplayProvider)
                return;

            _hasWarnedMissingDisplayProvider = true;
            Debug.LogWarning(
                $"{nameof(InventoryView)} '{UIHierarchyPathUtility.GetPath(transform)}' has no display provider. Connect a controller that handles {nameof(DisplayRefreshRequested)}.",
                this
            );
        }

        private List<ItemStack> FilterVisibleItems(List<ItemStack> items)
        {
            if (items == null)
                return null;

            if (!_showOnlyBaseItems)
                return items;

            return items.FindAll(stack => stack != null && IsBaseItem(stack.Data));
        }

        private static bool IsBaseItem(ItemData item)
        {
            if (item == null)
                return false;

            string id = Mathf.Abs(item.id).ToString();
            return id.Length >= 2 && id[1] == '0';
        }

        private void WarnMissingReferencesOnce() { }

        private void WarnMissingDetailPanelOnce()
        {
            if (_hasWarnedMissingDetailPanel)
                return;

            _hasWarnedMissingDetailPanel = true;
            Debug.LogWarning(
                $"{nameof(InventoryView)} '{name}' の必須参照 '{nameof(_detailPanel)}' が未設定です。アイテム詳細表示をスキップします。Inspectorで設定してください。",
                this
            );
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _tabGroup ??= GetComponentInChildren<TabGroup>(true);
            _detailPanel ??= GetComponentInChildren<ItemDetailPanel>(true);
        }
#endif

        private const float InitialSlotScale = 0.82f;
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
                ClearSelectionAfterRefresh();
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
            if (slot == null)
                return null;

            slot.gameObject.SetActive(true);
            slot.transform.SetAsLastSibling();
            slot.transform.DOKill();
            slot.SetReleaseSelectionOnOutsideClick(_releaseSelectionOnOutsideClick);
            slot.SetShowCount(_showItemCounts);
            slot.SetItem(stack);
            slot.SetCraftAssigned(IsCraftAssigned(stack));
            _visibleSlots.Add(slot);

            slot.transform.localScale = Vector3.one * InitialSlotScale;
            slot.transform.DOScale(Vector3.one, 0.2f)
                .SetEase(Ease.OutBack)
                .SetDelay(0.05f * index)
                .SetUpdate(true)
                .OnComplete(slot.RefreshCountLayout);

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

            var createdObject = Instantiate(_slotPrefab, _slotsRoot, false);
            if (!createdObject.TryGetComponent(out ItemSlot createdSlot))
            {
                Debug.LogError(
                    $"{nameof(InventoryView)} '{name}' のSlot Prefab '{_slotPrefab.name}'に{nameof(ItemSlot)}がありません。Prefabのルートへ追加してください。",
                    _slotPrefab
                );
                Destroy(createdObject);
                return null;
            }

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
                ClearSelectionAfterRefresh();
                return;
            }

            var selectedSlot = FindVisibleSlot(_selectedStack);
            if (selectedSlot != null)
            {
                _currentSelectedSlot = selectedSlot;
                _selectedStack = selectedSlot.Stack;
                selectedSlot.Select();
                _detailPanel?.Show(selectedSlot.Item);
                return;
            }

            if (_selectFirstSlotOnRefresh)
                SelectSlot(_visibleSlots[0]);
            else
                ClearSelectionAfterRefresh();
        }

        private void ClearSelectionAfterRefresh()
        {
            ClearSelection();
            _detailPanel?.Clear();
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
            Canvas.ForceUpdateCanvases();
            foreach (var slot in _visibleSlots)
                slot?.RefreshCountLayout();

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

        public void SelectSlot(ItemSlot slot)
        {
            if (slot == null || !_visibleSlots.Contains(slot) || !slot.gameObject.activeSelf)
                return;

            if (_currentSelectedSlot != null && _currentSelectedSlot != slot)
                _currentSelectedSlot.Deselect();

            slot.Select();
            _currentSelectedSlot = slot;
            _selectedStack = slot.Stack;
            _detailPanel?.Show(slot.Item);

            DisableNavigationOnce();
        }

        public void SelectSlotByClick(ItemSlot slot)
        {
            if (
                !_interactionEnabled
                || slot == null
                || !_visibleSlots.Contains(slot)
                || !slot.gameObject.activeSelf
            )
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
            OnSlotClicked?.Invoke(slot.Stack);
        }

        public void SelectSlotByDoubleClick(ItemSlot slot)
        {
            if (
                !_interactionEnabled
                || slot == null
                || !_visibleSlots.Contains(slot)
                || !slot.gameObject.activeSelf
            )
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
            OnSlotDoubleClicked?.Invoke(slot.Stack);
        }

        public void HighlightEquippedItem(ItemStack stack)
        {
            var slot = FindVisibleSlot(stack);
            if (slot != null)
                slot.SetEquipped(stack.IsEquipped);
        }

        public void UpdateItemEquippedState(ItemStack stack, bool isEquipped, bool keepSelected)
        {
            if (stack == null)
                return;

            var slot = FindVisibleSlot(stack);
            if (slot == null)
                return;

            slot.SetEquipped(isEquipped);

            if (!keepSelected)
                return;

            _selectedStack = stack;
            _currentSelectedSlot = slot;
            slot.Select();
        }

        public void SelectItem(ItemStack stack)
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _selectedStack = stack;
            _currentSelectedSlot = FindVisibleSlot(stack);
            _currentSelectedSlot?.Select();
        }

        public void ClearSelection()
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _currentSelectedSlot = null;
            _selectedStack = null;
            CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        public void ResetViewState()
        {
            ClearSelection();
            _detailPanel?.Clear();

            _tabGroup?.ResetToFirstTab();

            if (_tabGroup == null)
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        public void SetCraftAssignedItems(IEnumerable<ItemData> items)
        {
            _craftAssignedItems.Clear();
            _craftAssignedStacks.Clear();
            if (items != null)
            {
                foreach (var item in items)
                    if (item != null)
                        _craftAssignedItems.Add(item);
            }

            RefreshCraftAssignedSlots();
        }

        public void SetCraftAssignedStacks(IEnumerable<ItemStack> stacks)
        {
            _craftAssignedItems.Clear();
            _craftAssignedStacks.Clear();
            if (stacks != null)
            {
                foreach (var stack in stacks)
                    if (stack != null)
                        _craftAssignedStacks.Add(stack);
            }

            RefreshCraftAssignedSlots();
        }

        private bool IsCraftAssigned(ItemStack stack) =>
            stack != null
            && (
                _craftAssignedStacks.Contains(stack)
                || (stack.Data != null && _craftAssignedItems.Contains(stack.Data))
            );

        private void RefreshCraftAssignedSlots()
        {
            foreach (var slot in _visibleSlots)
                slot.SetCraftAssigned(IsCraftAssigned(slot.Stack));
        }

        public void ResetToTop()
        {
            KillScrollTween();
            if (_slotsRoot is RectTransform contentRect)
            {
                var scroll = contentRect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1f;
            }

            if (_visibleSlots.Count > 0)
                SelectSlot(_visibleSlots[0]);
        }

        private void DisableNavigationOnce()
        {
            if (_navigationDisabled || EventSystem.current == null)
                return;

            _previousSendNavigationEvents = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            _navigationDisabled = true;
        }

        private void RestoreNavigation()
        {
            if (!_navigationDisabled || EventSystem.current == null)
                return;

            EventSystem.current.sendNavigationEvents = _previousSendNavigationEvents;
            _navigationDisabled = false;
        }

        private void Update()
        {
            if (
                !isActiveAndEnabled
                || !_interactionEnabled
                || _currentSelectedSlot == null
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectSlotByOffset(1);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SelectSlotVertically(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SelectSlotVertically(1);
            else if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
                SubmitSelectedSlot();
        }

        public void SubmitSelectedSlot()
        {
            if (
                !_interactionEnabled
                || _currentSelectedSlot == null
                || _selectedStack == null
                || _selectedStack.Count <= 0
            )
                return;

            OnSlotSubmitted?.Invoke(_selectedStack);
        }

        private void SelectSlotByOffset(int offset)
        {
            if (!TryGetCurrentSlotIndex(out int currentIndex, out int slotCount))
                return;

            if (slotCount <= 1)
                return;

            int nextIndex = (currentIndex + offset + slotCount) % slotCount;
            SelectSlotAt(nextIndex);
        }

        private void SelectSlotVertically(int rowOffset)
        {
            if (!TryGetCurrentSlotIndex(out int currentIndex, out int slotCount))
                return;

            int columns = GetColumnCount();
            if (slotCount <= columns)
                return;

            int nextIndex = currentIndex + columns * rowOffset;

            if (nextIndex < 0)
                nextIndex = GetBottomIndexInColumn(currentIndex % columns, columns, slotCount);
            else if (nextIndex >= slotCount)
                nextIndex = currentIndex % columns;

            if (nextIndex == currentIndex)
                return;

            SelectSlotAt(nextIndex);
        }

        private bool TryGetCurrentSlotIndex(out int currentIndex, out int slotCount)
        {
            slotCount = _visibleSlots.Count;
            currentIndex = _visibleSlots.IndexOf(_currentSelectedSlot);
            if (
                _currentSelectedSlot == null
                || currentIndex < 0
                || slotCount <= 0
                || !_currentSelectedSlot.gameObject.activeSelf
            )
                return false;

            return true;
        }

        private int GetColumnCount()
        {
            if (
                _slotsRoot != null
                && _slotsRoot.TryGetComponent(out GridLayoutGroup grid)
                && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            )
            {
                return Mathf.Max(1, grid.constraintCount);
            }

            return Mathf.Max(1, _visibleSlots.Count);
        }

        private int GetBottomIndexInColumn(int column, int columns, int slotCount)
        {
            int bottomIndex = column;
            while (bottomIndex + columns < slotCount)
                bottomIndex += columns;

            if (bottomIndex != column || slotCount <= columns)
                return bottomIndex;

            return slotCount - 1;
        }

        private void SelectSlotAt(int index)
        {
            if (index < 0 || index >= _visibleSlots.Count)
                return;

            var slot = _visibleSlots[index];
            if (slot != null && slot.gameObject.activeInHierarchy)
                SelectSlot(slot);
        }
    }
}
