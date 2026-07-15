using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    public partial class Inventory : MonoBehaviour
    {
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

        private readonly List<ItemCategory> _activeCategories = new();
        private bool _navigationDisabled;
        private bool _previousSendNavigationEvents;
        private bool _started;
        private Coroutine _resetRoutine;
        private ItemSlot _currentSelectedSlot;
        private ItemStack _selectedStack;
        private readonly HashSet<ItemData> _craftAssignedItems = new();
        private readonly HashSet<ItemStack> _craftAssignedStacks = new();
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
            RefreshCurrentTab();
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
            RestoreNavigation();
            UnsubscribeFromInventoryChanges();
        }

        private void OnDestroy()
        {
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
                RefreshCurrentTab();
        }

        public void ClearFixedCategory()
        {
            _useFixedCategory = false;
            if (_tabGroup != null)
                _tabGroup.gameObject.SetActive(true);

            if (_started)
                RefreshCurrentTab();
        }

        public void RefreshCurrentTab()
        {
            if (_useFixedCategory)
            {
                var items = InventoryManager.Instance?.GetItemsByCategory(_fixedCategory);
                RefreshSlots(FilterVisibleItems(items));
                return;
            }

            OnTabSelected(_tabGroup != null ? _tabGroup.CurrentIndex : 0);
        }

        public void ResetToFirstTab()
        {
            if (_useFixedCategory)
                RefreshCurrentTab();
            else
                _tabGroup?.ResetToFirstTab();
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

            InventoryManager.Instance.InventoryChanged -= RefreshCurrentTab;
            InventoryManager.Instance.InventoryChanged += RefreshCurrentTab;
        }

        private void UnsubscribeFromInventoryChanges()
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= RefreshCurrentTab;
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

        private void OnTabSelected(int index)
        {
            if (_useFixedCategory)
            {
                var items = InventoryManager.Instance?.GetItemsByCategory(_fixedCategory);
                RefreshSlots(FilterVisibleItems(items));
                return;
            }

            if (index < 0 || index >= _activeCategories.Count)
                return;

            var categoryItems = InventoryManager.Instance?.GetItemsByCategory(
                _activeCategories[index]
            );
            RefreshSlots(FilterVisibleItems(categoryItems));
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
