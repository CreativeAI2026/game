using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory : MonoBehaviour
    {
        public enum ScrollRefreshMode
        {
            KeepPosition,
            ScrollToTop,
        }

        [SerializeField]
        private bool _selectFirstSlotOnRefresh = true;

        private bool _releaseSelectionOnOutsideClick = true;

        [Header("Tab")]
        [SerializeField]
        private TabGroup _tabGroup;

        [SerializeField]
        private List<ItemCategory> _categories;

        [SerializeField]
        private bool _useFixedCategory;

        [SerializeField]
        private ItemCategory _fixedCategory;

        [Header("Slots")]
        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private ItemSlot _slotPrefab;

        [Header("Detail")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private bool _showOnlyBaseItems = false;

        public event System.Action<ItemStack> OnSlotClicked;
        public event System.Action<ItemStack> OnSlotDoubleClicked;
        public event System.Action<ItemCategory, ScrollRefreshMode> ItemsRequested;

        private readonly List<ItemCategory> _activeCategories = new();
        private bool _navigationDisabled;
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
        private bool _hasWarnedMissingTabGroup;
        private bool _hasWarnedMissingDetailPanel;

        public void SetSelectFirstSlotOnRefresh(bool selectFirst) =>
            _selectFirstSlotOnRefresh = selectFirst;

        private void Awake()
        {
            WarnMissingReferencesOnce();

            if (_tabGroup != null)
                _tabGroup.OnTabSelected += OnTabSelected;
        }

        private void Start()
        {
            SubscribeToInventoryChanges();
            BuildActiveCategories();
            _started = true;
            RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        private void OnEnable()
        {
            SubscribeToInventoryChanges();

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
            UnsubscribeFromInventoryChanges();
        }

        private void OnDestroy()
        {
            KillScrollTween();
            RestoreNavigation();
            UnsubscribeFromInventoryChanges();

            if (_tabGroup != null)
                _tabGroup.OnTabSelected -= OnTabSelected;
        }

        public void SetFixedCategory(ItemCategory category, bool hideTabGroup = true)
        {
            _useFixedCategory = true;
            _fixedCategory = category;

            if (_tabGroup != null && hideTabGroup)
                _tabGroup.gameObject.SetActive(false);

            if (_started)
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        public void ClearFixedCategory()
        {
            _useFixedCategory = false;
            if (_tabGroup != null)
                _tabGroup.gameObject.SetActive(true);

            if (_started)
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
        }

        public void RefreshCurrentTab() => RefreshCurrentTab(ScrollRefreshMode.KeepPosition);

        public void SetItems(List<ItemStack> items) =>
            SetItems(items, ScrollRefreshMode.KeepPosition);

        public void SetItems(List<ItemStack> items, ScrollRefreshMode scrollMode) =>
            RefreshSlots(FilterVisibleItems(items), scrollMode);

        private void RefreshCurrentTab(ScrollRefreshMode scrollMode)
        {
            if (_useFixedCategory)
            {
                RequestItems(_fixedCategory, scrollMode);
                return;
            }

            RefreshTab(_tabGroup != null ? _tabGroup.CurrentIndex : 0, scrollMode);
        }

        public void ResetToFirstTab()
        {
            if (_useFixedCategory)
                RefreshCurrentTab(ScrollRefreshMode.ScrollToTop);
            else if (_tabGroup != null)
                _tabGroup?.ResetToFirstTab();
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

        private void SubscribeToInventoryChanges()
        {
            if (InventoryManager.Instance == null)
                return;

            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
        }

        private void UnsubscribeFromInventoryChanges()
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            if (ItemsRequested != null)
                return;

            RefreshCurrentTab(ScrollRefreshMode.KeepPosition);
        }

        private void BuildActiveCategories()
        {
            _activeCategories.Clear();
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_tabGroup == null || _tabGroup.IsEnabled(i))
                    _activeCategories.Add(_categories[i]);
            }
        }

        private void OnTabSelected(int index) => RefreshTab(index, ScrollRefreshMode.ScrollToTop);

        private void RefreshTab(int index, ScrollRefreshMode scrollMode)
        {
            if (_useFixedCategory)
            {
                RequestItems(_fixedCategory, scrollMode);
                return;
            }

            if (index < 0 || index >= _activeCategories.Count)
                return;

            RequestItems(_activeCategories[index], scrollMode);
        }

        private void RequestItems(ItemCategory category, ScrollRefreshMode scrollMode)
        {
            if (ItemsRequested != null)
            {
                ItemsRequested.Invoke(category, scrollMode);
                return;
            }

            var items = InventoryManager.Instance?.GetItemsByCategory(category);
            SetItems(items, scrollMode);
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

        private void WarnMissingReferencesOnce()
        {
            if (_tabGroup == null)
                WarnMissingTabGroupOnce();
            if (_detailPanel == null)
                WarnMissingDetailPanelOnce();
        }

        private void WarnMissingTabGroupOnce()
        {
            if (_hasWarnedMissingTabGroup)
                return;

            _hasWarnedMissingTabGroup = true;
            Debug.LogWarning(
                $"{nameof(Inventory)} '{name}' の必須参照 '{nameof(_tabGroup)}' が未設定です。タブイベントの購読をスキップし、タブなしの既存動作を使用します。Inspectorで設定してください。",
                this
            );
        }

        private void WarnMissingDetailPanelOnce()
        {
            if (_hasWarnedMissingDetailPanel)
                return;

            _hasWarnedMissingDetailPanel = true;
            Debug.LogWarning(
                $"{nameof(Inventory)} '{name}' の必須参照 '{nameof(_detailPanel)}' が未設定です。アイテム詳細表示をスキップします。Inspectorで設定してください。",
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
    }
}
