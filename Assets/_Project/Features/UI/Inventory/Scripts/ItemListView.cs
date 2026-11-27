using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 持ち物をタブごとに並べて選ばせる一覧。インベントリ・キャラクター・調合の各画面で使う。
    /// </summary>
    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.InventoryUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "Inventory"
    )]
    public sealed class ItemListView : MonoBehaviour
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

        [SerializeField]
        [Tooltip("選んでいるスロットを、一覧の外をクリックしたら選択解除するか")]
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
        private ItemDetailView _detailPanel;

        [SerializeField]
        private bool _showOnlyBaseItems = false;

        public event System.Action<ItemStack> OnSlotClicked;
        public event System.Action<ItemStack> OnSlotDoubleClicked;
        public event System.Action<ItemStack> OnSlotSubmitted;
        public ItemStack SelectedStack => _selectedStack;

        /// <summary>選んでいるアイテムが変わったとき(選択を外したときは null)。</summary>
        public event System.Action<ItemStack> SelectionChanged;

        /// <summary>
        /// 並べるアイテムを画面側で決めるときに設定する。未設定ならタブの定義に合わせて所持品から引く。
        /// </summary>
        public System.Func<List<ItemStack>> ItemsProvider { private get; set; }

        private bool _navigationDisabled;
        private bool _interactionEnabled = true;
        private bool _previousSendNavigationEvents;
        private bool _started;
        private Coroutine _resetRoutine;
        private ItemSlot _currentSelectedSlot;
        private ItemStack _selectedStack;
        private readonly List<ItemSlot> _visibleSlots = new();
        private readonly HashSet<ItemStack> _craftAssignedStacks = new();
        private ItemSlotPool _slotPool;

        public void SetSelectFirstSlotOnRefresh(bool selectFirst) =>
            _selectFirstSlotOnRefresh = selectFirst;

        public void SetShowItemCounts(bool show)
        {
            _showItemCounts = show;
            foreach (var slot in SlotPool.All)
                if (slot != null)
                    slot.SetShowCount(show);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            if (!enabled)
                CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        private ItemSlotPool SlotPool =>
            _slotPool ??= new ItemSlotPool(_slotsRoot, _slotPrefab, this);

        private void Awake()
        {
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

        public void SetItems(List<ItemStack> items, ScrollRefreshMode scrollMode) =>
            RefreshSlots(FilterVisibleItems(items), scrollMode);

        private void RefreshCurrentTab(ScrollRefreshMode scrollMode)
        {
            TabDefinition definition = _tabGroup != null ? _tabGroup.CurrentDefinition : null;
            if (definition == null && _tabGroup != null)
                definition = _tabGroup.GetDefinitionForButtonIndex(
                    Mathf.Max(0, _tabGroup.CurrentIndex)
                );
            SetItems(ResolveItems(definition), scrollMode);
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

        private void OnTabDefinitionSelected(int index, TabDefinition definition) =>
            SetItems(ResolveItems(definition), ScrollRefreshMode.ScrollToTop);

        private List<ItemStack> ResolveItems(TabDefinition definition)
        {
            if (ItemsProvider != null)
                return ItemsProvider();

            var inventory = InventoryManager.Instance;
            if (inventory == null)
                return null;

            switch (definition)
            {
                case AllItemsTabDefinition:
                    return inventory.GetAllItems();
                case InventoryTabDefinition tab:
                    return inventory.GetItemsByCategory(tab.Category);
                default:
                    return null;
            }
        }

        private List<ItemStack> FilterVisibleItems(List<ItemStack> items)
        {
            if (items == null)
                return null;

            if (!_showOnlyBaseItems)
                return items;

            return items.FindAll(stack => stack?.Data != null && stack.Data.IsBaseMaterial);
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_tabGroup == null)
                _tabGroup = GetComponentInChildren<TabGroup>(true);
            if (_detailPanel == null)
                _detailPanel = GetComponentInChildren<ItemDetailView>(true);
        }
#endif

        private const float InitialSlotScale = 0f;
        private Tween _scrollTween;

        private void RefreshSlots(List<ItemStack> items, ScrollRefreshMode scrollMode)
        {
            KillScrollTween();
            var scrollRect = GetScrollRect();
            float previousVerticalPosition =
                scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;
            float previousHorizontalPosition =
                scrollRect != null ? scrollRect.horizontalNormalizedPosition : 0f;
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
            var slot = SlotPool.Get();
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
                SetSelectedStack(selectedSlot.Stack);
                selectedSlot.Select();
                if (_detailPanel != null)
                    _detailPanel.Show(selectedSlot.Stack);
                return;
            }

            if (_selectFirstSlotOnRefresh)
            {
                CreativeAI.UI.SlotKeyboardFocus.Claim(this);
                SelectSlot(_visibleSlots[0]);
            }
            else
                ClearSelectionAfterRefresh();
        }

        private void ClearSelectionAfterRefresh()
        {
            ClearSelection();
            if (_detailPanel != null)
                _detailPanel.Clear();
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
            foreach (var slot in _visibleSlots)
                if (slot != null)
                    slot.RefreshCountLayout();

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
            SlotPool.Initialize();

            foreach (var slot in _visibleSlots)
                SlotPool.Return(slot);

            _visibleSlots.Clear();
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
            SetSelectedStack(slot.Stack);
            if (_detailPanel != null)
                _detailPanel.Show(slot.Stack);

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

        public void SelectItem(ItemStack stack)
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            SetSelectedStack(stack);
            _currentSelectedSlot = FindVisibleSlot(stack);
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Select();
        }

        public void ClearSelection()
        {
            if (_currentSelectedSlot != null)
                _currentSelectedSlot.Deselect();

            _currentSelectedSlot = null;
            SetSelectedStack(null);
            CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        private void SetSelectedStack(ItemStack stack)
        {
            if (_selectedStack == stack)
                return;
            _selectedStack = stack;
            SelectionChanged?.Invoke(stack);
        }

        public void ResetViewState()
        {
            ClearSelection();
            if (_detailPanel != null)
                _detailPanel.Clear();

            if (_tabGroup != null)
                _tabGroup.ResetToFirstTab();

            if (_tabGroup == null)
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        public void SetCraftAssignedStacks(IEnumerable<ItemStack> stacks)
        {
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
            stack != null && _craftAssignedStacks.Contains(stack);

        private void RefreshCraftAssignedSlots()
        {
            foreach (var slot in _visibleSlots)
                slot.SetCraftAssigned(IsCraftAssigned(slot.Stack));
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

            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
            {
                if (step.x < 0)
                    SelectSlotByOffset(-1);
                else if (step.x > 0)
                    SelectSlotByOffset(1);
                else if (step.y > 0)
                    SelectSlotVertically(-1);
                else if (step.y < 0)
                    SelectSlotVertically(1);
            }
            else if (UIInputActionMap.WasSubmitPressedThisFrame())
            {
                SubmitSelectedSlot();
            }
        }

        public bool SelectFirstVisibleSlotAndClaimFocus()
        {
            if (!_interactionEnabled || _visibleSlots.Count <= 0)
                return false;

            var target =
                _currentSelectedSlot != null && _visibleSlots.Contains(_currentSelectedSlot)
                    ? _currentSelectedSlot
                    : _visibleSlots[0];
            if (target == null || !target.gameObject.activeSelf)
                return false;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(target);
            OnSlotClicked?.Invoke(target.Stack);
            return true;
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
            {
                SelectSlot(slot);
                OnSlotClicked?.Invoke(slot.Stack);
            }
        }
    }
}
