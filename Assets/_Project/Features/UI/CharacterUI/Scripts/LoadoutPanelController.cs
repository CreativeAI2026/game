using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// Loadout画面の一覧、詳細、クイックアイテム3枠、装備3枠をまとめて制御する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadoutPanelController : MonoBehaviour
    {
        private enum Filter
        {
            All,
            Quick,
            Equipment,
        }

        private enum DetailAction
        {
            None,
            Assign,
            RemoveQuick,
            RemoveEquipment,
        }

        [SerializeField]
        private Color _tabNormalColor = new(0f, 0f, 0f, 0.39f);

        [SerializeField]
        private Color _tabSelectedColor = new(0.31f, 0.23f, 0.12f, 0.9f);

        [SerializeField]
        private Color _tabNormalTextColor = Color.white;

        [SerializeField]
        private Color _tabSelectedTextColor = new(1f, 0.72f, 0.33f, 1f);

        [Header("開発用プレビュー")]
        [SerializeField]
        [Tooltip("所持品が空の開発用シーンでは、確認用の所持品を生成します。")]
        private bool _seedPreviewInventoryWhenEmpty = true;

        [Header("詳細テキストアニメーション")]
        [SerializeField]
        private bool _animateDetailText = true;

        [SerializeField, Min(0f)]
        private float _detailTextStartDelay = 0.05f;

        [SerializeField, Min(0.001f)]
        private float _detailCharacterInterval = 0.035f;

        [SerializeField, Min(0f)]
        private float _detailSectionDelay = 0.06f;

        private readonly List<Button> _filterButtons = new();
        private readonly List<UnityAction> _filterActions = new();
        private readonly List<LoadoutAssignmentSlotView> _quickSlots = new();
        private readonly List<LoadoutAssignmentSlotView> _equipmentSlots = new();
        private readonly ItemStack[] _equipmentAssignments = new ItemStack[3];

        private InventoryView _inventory;
        private Image _detailIcon;
        private TMP_Text _detailName;
        private TMP_Text _detailDescription;
        private TMP_Text _detailCount;
        private Button _actionButton;
        private TMP_Text _actionLabel;
        private TMP_Text _equipmentTitle;

        private Filter _filter;
        private DetailAction _detailAction;
        private ItemStack _selectedStack;
        private int _selectedQuickSlot;
        private int _selectedEquipmentSlot;
        private bool _initialized;
        private bool _eventsBound;
        private bool _suppressEquipmentRefresh;
        private Coroutine _refreshRoutine;
        private InventoryManager _subscribedInventoryManager;
        private TmpTypewriterPlayer _typewriter;

        private InventoryManager ActiveInventoryManager =>
            _subscribedInventoryManager != null
                ? _subscribedInventoryManager
                : InventoryManager.Instance;

        private void Awake()
        {
            _typewriter = new TmpTypewriterPlayer(this);
            ResolveReferences();
            Initialize();
        }

        private void OnEnable()
        {
            _typewriter ??= new TmpTypewriterPlayer(this);
            ResolveReferences();
            if (!Initialize())
                return;

            BindDataEvents();
            RestartDeferredRefresh();
        }

        private void OnDisable()
        {
            _typewriter?.CompleteAll();
            StopDeferredRefresh();
            UnbindDataEvents();
        }

        private void OnDestroy()
        {
            UnbindUiEvents();
            UnbindDataEvents();
        }

        private bool Initialize()
        {
            if (_initialized)
                return true;

            ResolveFilterButtons();
            ResolveAssignmentSlots();
            if (!ValidateRequiredReferences())
                return false;

            BindUiEvents();

            _inventory?.SetSelectFirstSlotOnRefresh(false);
            _inventory?.SetShowItemCounts(true);
            if (_equipmentTitle != null)
                _equipmentTitle.text = "EQUIPMENT";

            _selectedQuickSlot = 0;
            _selectedEquipmentSlot = 0;
            _filter = Filter.All;
            _initialized = true;

            RefreshTabVisuals();
            ShowEmptyDetail();
            return true;
        }

        private bool ValidateRequiredReferences()
        {
            if (_inventory == null)
            {
                Debug.LogError(
                    $"{nameof(LoadoutPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}' に InventoryView がありません。InventoryArea 以下へ配置してください。",
                    this
                );
                enabled = false;
                return false;
            }

            var warnings = new List<string>();
            if (_filterButtons.Count != 3)
                warnings.Add($"カテゴリボタン3個（現在 {_filterButtons.Count} 個）");
            if (_quickSlots.Count != 3)
                warnings.Add($"QuickItemArea のスロット3個（現在 {_quickSlots.Count} 個）");
            if (_equipmentSlots.Count != 3)
                warnings.Add($"EquipArea のスロット3個（現在 {_equipmentSlots.Count} 個）");
            if (_detailIcon == null)
                warnings.Add("詳細アイコン");
            if (_detailName == null)
                warnings.Add("詳細アイテム名");
            if (_detailDescription == null)
                warnings.Add("詳細説明文");
            if (_detailCount == null)
                warnings.Add("詳細所持数");
            if (_actionButton == null || _actionLabel == null)
                warnings.Add("SETボタンとラベル");

            if (warnings.Count > 0)
                Debug.LogWarning(
                    $"{nameof(LoadoutPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}' の補助UIが一部不足しています。一覧表示は続行します: {string.Join(", ", warnings)}",
                    this
                );

            return true;
        }

        private void ResolveReferences()
        {
            _inventory ??= transform
                .Find("InventoryArea/ScrollViewArea")
                ?.GetComponentInChildren<InventoryView>(true);
            _detailIcon ??= transform
                .Find("InventoryArea/DetailArea/ItemArea/Background/Icon")
                ?.GetComponent<Image>();
            _detailName ??= transform
                .Find("InventoryArea/DetailArea/DescriptionArea/Title")
                ?.GetComponent<TMP_Text>();
            _detailDescription ??= transform
                .Find("InventoryArea/DetailArea/DescriptionArea/Description")
                ?.GetComponent<TMP_Text>();
            _detailCount ??= transform
                .Find("InventoryArea/DetailArea/Text (TMP)")
                ?.GetComponent<TMP_Text>();
            _actionButton ??= transform
                .Find("InventoryArea/DetailArea/Button")
                ?.GetComponent<Button>();
            _actionLabel ??= _actionButton?.GetComponentInChildren<TMP_Text>(true);
            _equipmentTitle ??= transform
                .Find("EquipArea/TitleArea/Title")
                ?.GetComponent<TMP_Text>();
        }

        private void ResolveFilterButtons()
        {
            if (_filterButtons.Count > 0)
                return;

            Transform root = transform.Find("InventoryArea/TabArea");
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                Button button = root.GetChild(i).GetComponentInChildren<Button>(true);
                if (button != null)
                    _filterButtons.Add(button);
            }
        }

        private void ResolveAssignmentSlots()
        {
            if (_quickSlots.Count == 0)
                CollectSlots(transform.Find("QuickItemArea/SlotArea"), _quickSlots);
            if (_equipmentSlots.Count == 0)
                CollectSlots(transform.Find("EquipArea/SlotArea"), _equipmentSlots);

            ConfigureSlotDragSources(_quickSlots, ItemSlotDragOrigin.Quick);
            ConfigureSlotDragSources(_equipmentSlots, ItemSlotDragOrigin.Equipment);
        }

        private static void ConfigureSlotDragSources(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            ItemSlotDragOrigin origin
        )
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].ConfigureDragSource(origin, i);
        }

        private static void CollectSlots(
            Transform root,
            List<LoadoutAssignmentSlotView> destination
        )
        {
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                LoadoutAssignmentSlotView slot = root.GetChild(i)
                    .GetComponent<LoadoutAssignmentSlotView>();
                if (slot != null)
                    destination.Add(slot);
            }
        }

        private void BindUiEvents()
        {
            if (_eventsBound)
                return;

            for (int i = 0; i < _filterButtons.Count; i++)
            {
                int index = i;
                UnityAction action = () => SelectFilter(index);
                _filterActions.Add(action);
                _filterButtons[i].onClick.AddListener(action);
            }

            foreach (LoadoutAssignmentSlotView slot in _quickSlots)
            {
                slot.Clicked += OnQuickSlotClicked;
                slot.DoubleClicked += OnQuickSlotDoubleClicked;
                slot.Dropped += OnAssignmentSlotDropped;
                slot.DragEntered += OnAssignmentSlotDragEntered;
                slot.DragExited += OnAssignmentSlotDragExited;
            }
            foreach (LoadoutAssignmentSlotView slot in _equipmentSlots)
            {
                slot.Clicked += OnEquipmentSlotClicked;
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;
                slot.Dropped += OnAssignmentSlotDropped;
                slot.DragEntered += OnAssignmentSlotDragEntered;
                slot.DragExited += OnAssignmentSlotDragExited;
            }

            if (_inventory != null)
            {
                _inventory.OnSlotClicked += OnInventorySlotClicked;
                _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
                _inventory.DisplayRefreshRequested += OnInventoryRefreshRequested;
            }

            ItemSlotDragContext.Started += OnItemDragStarted;

            _actionButton?.onClick.AddListener(ExecuteDetailAction);
            _eventsBound = true;
        }

        private void UnbindUiEvents()
        {
            if (!_eventsBound)
                return;

            for (int i = 0; i < _filterButtons.Count && i < _filterActions.Count; i++)
                _filterButtons[i]?.onClick.RemoveListener(_filterActions[i]);
            _filterActions.Clear();
            foreach (LoadoutAssignmentSlotView slot in _quickSlots)
            {
                if (slot != null)
                {
                    slot.Clicked -= OnQuickSlotClicked;
                    slot.DoubleClicked -= OnQuickSlotDoubleClicked;
                    slot.Dropped -= OnAssignmentSlotDropped;
                    slot.DragEntered -= OnAssignmentSlotDragEntered;
                    slot.DragExited -= OnAssignmentSlotDragExited;
                }
            }
            foreach (LoadoutAssignmentSlotView slot in _equipmentSlots)
            {
                if (slot != null)
                {
                    slot.Clicked -= OnEquipmentSlotClicked;
                    slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
                    slot.Dropped -= OnAssignmentSlotDropped;
                    slot.DragEntered -= OnAssignmentSlotDragEntered;
                    slot.DragExited -= OnAssignmentSlotDragExited;
                }
            }

            if (_inventory != null)
            {
                _inventory.OnSlotClicked -= OnInventorySlotClicked;
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
                _inventory.DisplayRefreshRequested -= OnInventoryRefreshRequested;
            }

            ItemSlotDragContext.Started -= OnItemDragStarted;

            _actionButton?.onClick.RemoveListener(ExecuteDetailAction);
            _eventsBound = false;
        }

        private void OnItemDragStarted(ItemSlotDragPayload _)
        {
            _inventory?.ClearSelection();
            ClearAssignmentPreviews();
            SetSelectedSlot(_quickSlots, -1);
            SetSelectedSlot(_equipmentSlots, -1);
        }

        private void BindDataEvents()
        {
            InventoryManager manager = ResolveInventoryManager();
            if (_subscribedInventoryManager != manager)
            {
                UnbindInventoryEvents();
                _subscribedInventoryManager = manager;
            }

            if (_subscribedInventoryManager != null)
            {
                _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
                _subscribedInventoryManager.InventoryChanged += OnInventoryChanged;
                _subscribedInventoryManager.QuickFoodChanged -= OnQuickFoodChanged;
                _subscribedInventoryManager.QuickFoodChanged += OnQuickFoodChanged;
            }

            InventoryManager.EquipmentChanged -= OnEquipmentChanged;
            InventoryManager.EquipmentChanged += OnEquipmentChanged;
        }

        private void UnbindDataEvents()
        {
            UnbindInventoryEvents();

            InventoryManager.EquipmentChanged -= OnEquipmentChanged;
        }

        private void UnbindInventoryEvents()
        {
            if (_subscribedInventoryManager == null)
                return;

            _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
            _subscribedInventoryManager.QuickFoodChanged -= OnQuickFoodChanged;
            _subscribedInventoryManager = null;
        }

        private InventoryManager ResolveInventoryManager()
        {
            InventoryManager manager = InventoryManager.Instance;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_seedPreviewInventoryWhenEmpty)
            {
                manager ??= InventoryManager.EnsureResident();
                if (manager.GetAllItems().Count == 0)
                    manager.SeedTestItems();
            }
#endif
            return manager;
        }

        private void RestartDeferredRefresh()
        {
            StopDeferredRefresh();
            _refreshRoutine = StartCoroutine(RefreshNextFrame());
        }

        private void StopDeferredRefresh()
        {
            if (_refreshRoutine == null)
                return;

            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            BindDataEvents();
            SyncAll();
            _refreshRoutine = null;
        }

        private void SyncAll()
        {
            RefreshInventory(InventoryView.ScrollRefreshMode.ScrollToTop);
            RefreshQuickSlots();
            SyncEquipmentAssignments();
            RefreshEquipmentSlots();
            RefreshTargetSelection();
        }

        private void SelectFilter(int index)
        {
            if (index < 0 || index > (int)Filter.Equipment)
                return;

            _filter = (Filter)index;
            _selectedStack = null;
            RefreshTabVisuals();
            RefreshInventory(InventoryView.ScrollRefreshMode.ScrollToTop);
            RefreshTargetSelection();
            ShowEmptyDetail();
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < _filterButtons.Count; i++)
            {
                Button button = _filterButtons[i];
                bool selected = i == (int)_filter;
                if (button.targetGraphic is Image background)
                    background.color = selected ? _tabSelectedColor : _tabNormalColor;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.color = selected ? _tabSelectedTextColor : _tabNormalTextColor;
            }
        }

        private void OnInventoryRefreshRequested(
            TabDefinition _definition,
            int _tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        ) => RefreshInventory(scrollMode);

        private void RefreshInventory(InventoryView.ScrollRefreshMode scrollMode)
        {
            if (_inventory == null)
                return;

            List<ItemStack> items = ActiveInventoryManager?.GetAllItems();
            if (items == null)
            {
                _inventory.SetItems(null, scrollMode);
                return;
            }

            List<ItemStack> filtered = items.Where(IsVisibleInCurrentFilter).ToList();
            _inventory.SetItems(filtered, scrollMode);
        }

        private bool IsVisibleInCurrentFilter(ItemStack stack)
        {
            ItemData data = stack?.Data;
            if (data == null)
                return false;

            return _filter switch
            {
                Filter.Quick => data.category == ItemCategory.Food,
                Filter.Equipment => data.category == ItemCategory.Equipment,
                _ => data.category == ItemCategory.Food || data.category == ItemCategory.Equipment,
            };
        }

        private void OnInventorySlotClicked(ItemStack stack)
        {
            if (stack?.Data == null)
                return;

            if (_selectedStack == stack && _detailAction == DetailAction.Assign)
                return;

            _selectedStack = stack;
            ShowDetail(stack, DetailAction.Assign);
            SelectTargetFor(stack.Data.category);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            OnInventorySlotClicked(stack);
            AssignSelectedStack();
        }

        private void SelectTargetFor(ItemCategory category)
        {
            if (category == ItemCategory.Food)
            {
                _selectedQuickSlot = FindPreferredSlot(_quickSlots, _selectedQuickSlot);
                SetSelectedSlot(_quickSlots, _selectedQuickSlot);
                SetSelectedSlot(_equipmentSlots, -1);
            }
            else if (category == ItemCategory.Equipment)
            {
                _selectedEquipmentSlot = FindPreferredSlot(_equipmentSlots, _selectedEquipmentSlot);
                SetSelectedSlot(_quickSlots, -1);
                SetSelectedSlot(_equipmentSlots, _selectedEquipmentSlot);
            }
        }

        private static int FindPreferredSlot(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            int selectedIndex
        )
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Stack == null)
                    return i;

            return slots.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, slots.Count - 1);
        }

        private void OnQuickSlotClicked(LoadoutAssignmentSlotView slot)
        {
            _selectedQuickSlot = _quickSlots.IndexOf(slot);
            SetSelectedSlot(_quickSlots, _selectedQuickSlot);
            SetSelectedSlot(_equipmentSlots, -1);

            if (slot.Stack != null)
            {
                if (_selectedStack == slot.Stack && _detailAction == DetailAction.RemoveQuick)
                    return;

                _selectedStack = slot.Stack;
                _inventory?.SelectItem(slot.Stack);
                ShowDetail(slot.Stack, DetailAction.RemoveQuick);
            }
            else if (_selectedStack?.Data?.category == ItemCategory.Food)
            {
                if (_detailAction == DetailAction.Assign)
                    return;

                ShowDetail(_selectedStack, DetailAction.Assign);
            }
            else
            {
                ShowEmptyDetail();
            }
        }

        private void OnEquipmentSlotClicked(LoadoutAssignmentSlotView slot)
        {
            _selectedEquipmentSlot = _equipmentSlots.IndexOf(slot);
            SetSelectedSlot(_quickSlots, -1);
            SetSelectedSlot(_equipmentSlots, _selectedEquipmentSlot);

            if (slot.Stack != null)
            {
                if (_selectedStack == slot.Stack && _detailAction == DetailAction.RemoveEquipment)
                    return;

                _selectedStack = slot.Stack;
                _inventory?.SelectItem(slot.Stack);
                ShowDetail(slot.Stack, DetailAction.RemoveEquipment);
            }
            else if (_selectedStack?.Data?.category == ItemCategory.Equipment)
            {
                if (_detailAction == DetailAction.Assign)
                    return;

                ShowDetail(_selectedStack, DetailAction.Assign);
            }
            else
            {
                ShowEmptyDetail();
            }
        }

        private void OnQuickSlotDoubleClicked(LoadoutAssignmentSlotView slot)
        {
            int index = _quickSlots.IndexOf(slot);
            if (index < 0 || slot.Stack == null)
                return;

            _selectedQuickSlot = index;
            RemoveQuickItem();
        }

        private void OnEquipmentSlotDoubleClicked(LoadoutAssignmentSlotView slot)
        {
            int index = _equipmentSlots.IndexOf(slot);
            if (index < 0 || slot.Stack == null)
                return;

            _selectedEquipmentSlot = index;
            RemoveEquipment();
        }

        private void OnAssignmentSlotDropped(
            LoadoutAssignmentSlotView target,
            ItemSlotDragPayload payload
        )
        {
            int quickTarget = _quickSlots.IndexOf(target);
            if (quickTarget >= 0)
            {
                DropIntoQuickSlot(quickTarget, payload);
                return;
            }

            int equipmentTarget = _equipmentSlots.IndexOf(target);
            if (equipmentTarget >= 0)
                DropIntoEquipmentSlot(equipmentTarget, payload);
        }

        private void OnAssignmentSlotDragEntered(
            LoadoutAssignmentSlotView target,
            ItemSlotDragPayload payload
        )
        {
            ClearAssignmentPreviews();
            target.ShowPreview(payload.Stack);

            IReadOnlyList<LoadoutAssignmentSlotView> sourceSlots = payload.Origin switch
            {
                ItemSlotDragOrigin.Quick => _quickSlots,
                ItemSlotDragOrigin.Equipment => _equipmentSlots,
                _ => null,
            };
            if (
                sourceSlots == null
                || payload.SlotIndex < 0
                || payload.SlotIndex >= sourceSlots.Count
            )
                return;

            LoadoutAssignmentSlotView source = sourceSlots[payload.SlotIndex];
            if (source != target && target.Stack != null)
                source.ShowPreview(target.Stack);
        }

        private void OnAssignmentSlotDragExited(LoadoutAssignmentSlotView _) =>
            ClearAssignmentPreviews();

        private void ClearAssignmentPreviews()
        {
            foreach (LoadoutAssignmentSlotView slot in _quickSlots)
                slot?.ClearPreview();
            foreach (LoadoutAssignmentSlotView slot in _equipmentSlots)
                slot?.ClearPreview();
        }

        private void DropIntoQuickSlot(int targetIndex, ItemSlotDragPayload payload)
        {
            if (payload.Stack?.Data?.category != ItemCategory.Food)
                return;

            _selectedQuickSlot = targetIndex;
            SetSelectedSlot(_quickSlots, targetIndex);
            SetSelectedSlot(_equipmentSlots, -1);

            if (payload.Origin == ItemSlotDragOrigin.Quick)
            {
                if (ActiveInventoryManager?.SwapQuickFood(payload.SlotIndex, targetIndex) != true)
                    return;

                RefreshQuickSlots();
                _selectedStack = payload.Stack;
                _inventory?.SelectItem(payload.Stack);
                ShowDetail(payload.Stack, DetailAction.RemoveQuick);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignQuickItem();
            _inventory?.SelectItem(payload.Stack);
        }

        private void DropIntoEquipmentSlot(int targetIndex, ItemSlotDragPayload payload)
        {
            if (payload.Stack?.Data is not EquipmentData)
                return;

            _selectedEquipmentSlot = targetIndex;
            SetSelectedSlot(_quickSlots, -1);
            SetSelectedSlot(_equipmentSlots, targetIndex);

            if (payload.Origin == ItemSlotDragOrigin.Equipment)
            {
                int sourceIndex = payload.SlotIndex;
                if (
                    sourceIndex < 0
                    || sourceIndex >= _equipmentAssignments.Length
                    || sourceIndex == targetIndex
                )
                    return;

                (_equipmentAssignments[sourceIndex], _equipmentAssignments[targetIndex]) = (
                    _equipmentAssignments[targetIndex],
                    _equipmentAssignments[sourceIndex]
                );
                RefreshEquipmentSlots();
                _selectedStack = payload.Stack;
                _inventory?.SelectItem(payload.Stack);
                ShowDetail(payload.Stack, DetailAction.RemoveEquipment);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignEquipment();
            _inventory?.SelectItem(payload.Stack);
        }

        private static void SetSelectedSlot(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            int selectedIndex
        )
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetSelected(i == selectedIndex);
        }

        private void RefreshTargetSelection()
        {
            if (_selectedStack?.Data?.category == ItemCategory.Equipment)
            {
                SetSelectedSlot(_quickSlots, -1);
                SetSelectedSlot(_equipmentSlots, _selectedEquipmentSlot);
            }
            else
            {
                SetSelectedSlot(_quickSlots, _selectedQuickSlot);
                SetSelectedSlot(_equipmentSlots, -1);
            }
        }

        private void ExecuteDetailAction()
        {
            switch (_detailAction)
            {
                case DetailAction.Assign:
                    AssignSelectedStack();
                    break;
                case DetailAction.RemoveQuick:
                    RemoveQuickItem();
                    break;
                case DetailAction.RemoveEquipment:
                    RemoveEquipment();
                    break;
            }
        }

        private void AssignSelectedStack()
        {
            if (_selectedStack?.Data == null)
                return;

            if (_selectedStack.Data.category == ItemCategory.Food)
                AssignQuickItem();
            else if (_selectedStack.Data.category == ItemCategory.Equipment)
                AssignEquipment();
        }

        private void AssignQuickItem()
        {
            if (_selectedQuickSlot < 0 || _selectedQuickSlot >= _quickSlots.Count)
                return;

            if (ActiveInventoryManager?.SetQuickFood(_selectedQuickSlot, _selectedStack) != true)
                return;

            RefreshQuickSlots();
            ShowDetail(_selectedStack, DetailAction.Assign);
        }

        private void AssignEquipment()
        {
            if (
                _selectedEquipmentSlot < 0
                || _selectedEquipmentSlot >= _equipmentAssignments.Length
                || _selectedStack.Data is not EquipmentData
            )
                return;

            InventoryManager manager = ActiveInventoryManager;
            if (manager == null)
                return;

            _suppressEquipmentRefresh = true;
            try
            {
                for (int i = 0; i < _equipmentAssignments.Length; i++)
                {
                    if (i != _selectedEquipmentSlot && _equipmentAssignments[i] == _selectedStack)
                        _equipmentAssignments[i] = null;
                }

                ItemStack previous = _equipmentAssignments[_selectedEquipmentSlot];
                if (previous != null && previous != _selectedStack)
                    manager.SetEquipped(previous, false);

                manager.SetEquipped(_selectedStack, true);
                if (_selectedStack.IsEquipped)
                    _equipmentAssignments[_selectedEquipmentSlot] = _selectedStack;
            }
            finally
            {
                _suppressEquipmentRefresh = false;
            }

            SyncEquipmentAssignments();
            RefreshEquipmentSlots();
            RefreshInventory(InventoryView.ScrollRefreshMode.KeepPosition);
            ShowDetail(_selectedStack, DetailAction.Assign);
        }

        private void RemoveQuickItem()
        {
            if (_selectedQuickSlot < 0 || _selectedQuickSlot >= _quickSlots.Count)
                return;

            ActiveInventoryManager?.ClearQuickFood(_selectedQuickSlot);
            _selectedStack = null;
            _inventory?.ClearSelection();
            RefreshQuickSlots();
            ShowEmptyDetail();
        }

        private void RemoveEquipment()
        {
            if (
                _selectedEquipmentSlot < 0
                || _selectedEquipmentSlot >= _equipmentAssignments.Length
            )
                return;

            ItemStack stack = _equipmentAssignments[_selectedEquipmentSlot];
            _suppressEquipmentRefresh = true;
            try
            {
                ActiveInventoryManager?.SetEquipped(stack, false);
                _equipmentAssignments[_selectedEquipmentSlot] = null;
            }
            finally
            {
                _suppressEquipmentRefresh = false;
            }

            _selectedStack = null;
            _inventory?.ClearSelection();
            RefreshEquipmentSlots();
            RefreshInventory(InventoryView.ScrollRefreshMode.KeepPosition);
            ShowEmptyDetail();
        }

        private void RefreshQuickSlots()
        {
            IReadOnlyList<ItemStack> assigned = ActiveInventoryManager?.GetQuickFoodSlots();
            for (int i = 0; i < _quickSlots.Count; i++)
            {
                ItemStack stack = assigned != null && i < assigned.Count ? assigned[i] : null;
                _quickSlots[i].SetStack(stack);
            }
        }

        private void SyncEquipmentAssignments()
        {
            List<ItemStack> allItems = ActiveInventoryManager?.GetAllItems();
            if (allItems == null)
                return;

            for (int i = 0; i < _equipmentAssignments.Length; i++)
            {
                ItemStack stack = _equipmentAssignments[i];
                if (stack != null && (!stack.IsEquipped || !allItems.Contains(stack)))
                    _equipmentAssignments[i] = null;
            }

            foreach (
                ItemStack stack in allItems.Where(item =>
                    item?.Data is EquipmentData && item.IsEquipped
                )
            )
            {
                if (_equipmentAssignments.Contains(stack))
                    continue;

                int empty = System.Array.IndexOf(_equipmentAssignments, null);
                if (empty < 0)
                    break;
                _equipmentAssignments[empty] = stack;
            }
        }

        private void RefreshEquipmentSlots()
        {
            for (int i = 0; i < _equipmentSlots.Count; i++)
            {
                ItemStack stack =
                    i < _equipmentAssignments.Length ? _equipmentAssignments[i] : null;
                _equipmentSlots[i].SetStack(stack);
            }
        }

        private void ShowDetail(ItemStack stack, DetailAction action)
        {
            ItemData item = stack?.Data;
            if (item == null)
            {
                ShowEmptyDetail();
                return;
            }

            if (_detailIcon != null)
            {
                _detailIcon.sprite = item.icon;
                _detailIcon.color = item.icon != null ? Color.white : Color.clear;
                _detailIcon.preserveAspect = true;
            }

            PlayDetailText(_detailName, item.itemName, 0);
            PlayDetailText(_detailDescription, item.description, 1);
            if (_detailCount != null)
                _detailCount.text = $"x{stack.Count}";

            SetDetailAction(action);
        }

        private void ShowEmptyDetail()
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = null;
                _detailIcon.color = Color.clear;
            }

            PlayDetailText(_detailName, "アイテムを選択", 0);
            PlayDetailText(_detailDescription, "一覧からアイテムを選んでください。", 1);
            if (_detailCount != null)
                _detailCount.text = string.Empty;

            SetDetailAction(DetailAction.None);
        }

        private void PlayDetailText(TMP_Text text, string value, int sectionIndex)
        {
            _typewriter ??= new TmpTypewriterPlayer(this);
            _typewriter.Play(
                text,
                value,
                _detailTextStartDelay + _detailSectionDelay * sectionIndex,
                _detailCharacterInterval,
                _animateDetailText
            );
        }

        private void SetDetailAction(DetailAction action)
        {
            _detailAction = action;
            if (_actionButton != null)
                _actionButton.interactable = action != DetailAction.None;
            if (_actionLabel != null)
                _actionLabel.text = action
                    is DetailAction.RemoveQuick
                        or DetailAction.RemoveEquipment
                    ? "REMOVE"
                    : "SET";
        }

        private void OnInventoryChanged()
        {
            RefreshInventory(InventoryView.ScrollRefreshMode.KeepPosition);
            RefreshQuickSlots();
            SyncEquipmentAssignments();
            RefreshEquipmentSlots();
        }

        private void OnQuickFoodChanged() => RefreshQuickSlots();

        private void OnEquipmentChanged()
        {
            if (_suppressEquipmentRefresh)
                return;

            SyncEquipmentAssignments();
            RefreshEquipmentSlots();
            RefreshInventory(InventoryView.ScrollRefreshMode.KeepPosition);
        }
    }
}
