using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace CreativeAI.UI.InventoryUI
{
    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.InventoryUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "Inventory"
    )]
    public partial class InventoryView : MonoBehaviour
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

        private void WarnMissingReferencesOnce()
        {
            if (_detailPanel == null)
                WarnMissingDetailPanelOnce();
        }

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
    }
}
