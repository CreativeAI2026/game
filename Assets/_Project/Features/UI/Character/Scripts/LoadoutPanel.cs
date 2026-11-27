using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// キャラクター画面(Loadout)の進行役。持ち物の一覧・絞り込みタブ・キー操作をまとめ、
    /// 詳細欄(<see cref="LoadoutDetailPanel"/>)・食材/装備の枠(<see cref="LoadoutAssignment"/>)・
    /// ドラッグ&amp;ドロップ(<see cref="LoadoutDragDrop"/>)をつなぐ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadoutPanel : MonoBehaviour
    {
        private enum Filter
        {
            Quick,
            Equipment,
        }

        /// <summary>持ち物の絞り込みタブ1つ分。</summary>
        [Serializable]
        private struct FilterTab
        {
            public Button button;
            public TMP_Text label;

            [Tooltip("そのカテゴリの所持数")]
            public TMP_Text count;
        }

        [Header("部品")]
        [SerializeField]
        private ItemListView _inventory;

        [SerializeField]
        [Tooltip("並び順 = 食材、装備")]
        private FilterTab[] _filterTabs = Array.Empty<FilterTab>();

        [SerializeField]
        private LoadoutAssignmentSlotView[] _quickSlots = Array.Empty<LoadoutAssignmentSlotView>();

        [SerializeField]
        private LoadoutAssignmentSlotView[] _equipmentSlots =
            Array.Empty<LoadoutAssignmentSlotView>();

        [SerializeField]
        private LoadoutDetailView _detail;

        [SerializeField]
        private LoadoutDropErrorFeedback _dropErrorFeedback;

        [Header("見た目")]
        [SerializeField]
        private Color _tabNormalColor = new(0f, 0f, 0f, 0.39f);

        [SerializeField]
        private Color _tabSelectedColor = new(0.31f, 0.23f, 0.12f, 0.9f);

        [SerializeField]
        private Color _tabNormalTextColor = Color.white;

        [SerializeField]
        private Color _tabSelectedTextColor = new Color32(0xff, 0xc3, 0x46, 0xff);

        [Header("Slot Appearance")]
        [SerializeField, Min(0f)]
        private float _slotAppearDuration = 0.2f;

        [SerializeField, Min(0f)]
        private float _slotAppearStagger = 0.05f;

        private readonly List<UnityAction> _filterActions = new();

        private LoadoutAssignment _slots;
        private LoadoutDragDrop _dragDrop;

        private Filter _filter;
        private ItemStack _selectedStack;
        private bool _initialized;
        private bool _eventsBound;
        private Coroutine _refreshRoutine;
        private InventoryManager _subscribedInventoryManager;
        private QuickFoodSlots _subscribedQuickFood;
        private EquipmentSlots _subscribedEquipment;

        private InventoryManager ActiveInventoryManager =>
            _subscribedInventoryManager != null
                ? _subscribedInventoryManager
                : InventoryManager.Instance;

        private void Awake()
        {
            _slots = new LoadoutAssignment(_quickSlots, _equipmentSlots);
            _dragDrop = new LoadoutDragDrop(_slots, _dropErrorFeedback);
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();

            _selectedStack = null;
            _inventory.ClearSelection();
            ShowEmptyDetail(replayText: true);
            _slots.PlayAppearance(_slotAppearDuration, _slotAppearStagger, this);
            BindDataEvents();
            RestartDeferredRefresh();
        }

        private void OnDisable()
        {
            _slots.StopAppearance();
            _detail.CompleteText();
            StopDeferredRefresh();
            UnbindDataEvents();
        }

        private void OnDestroy()
        {
            _slots.StopAppearance();
            UnbindUIEvents();
            UnbindDataEvents();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            BindUIEvents();

            _inventory.SetSelectFirstSlotOnRefresh(false);
            _inventory.SetShowItemCounts(true);

            _slots.SelectedQuick = 0;
            _slots.SelectedEquipment = 0;
            _filter = Filter.Quick;
            _initialized = true;

            RefreshTabVisuals();
            ShowEmptyDetail();
        }

        private static ItemCategory? GetCategory(ItemStack stack)
        {
            ItemData data = stack?.Data;
            return data != null ? data.category : null;
        }

        private void BindUIEvents()
        {
            if (_eventsBound)
                return;

            for (int i = 0; i < _filterTabs.Length; i++)
            {
                int index = i;
                UnityAction action = () => SelectFilter(index);
                _filterActions.Add(action);
                _filterTabs[i].button.onClick.AddListener(action);
            }

            foreach (LoadoutAssignmentSlotView slot in _slots.QuickSlots)
            {
                slot.Clicked += OnQuickSlotClicked;
                slot.DoubleClicked += OnQuickSlotDoubleClicked;
                slot.Dropped += OnAssignmentSlotDropped;
            }
            foreach (LoadoutAssignmentSlotView slot in _slots.EquipmentSlots)
            {
                slot.Clicked += OnEquipmentSlotClicked;
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;
                slot.Dropped += OnAssignmentSlotDropped;
            }
            _dragDrop.Bind();
            ItemSlotDragContext.Started += OnItemDragStarted;
            ItemSlotDragContext.Ended += OnItemDragEnded;

            _inventory.OnSlotClicked += OnInventorySlotClicked;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted += OnInventorySlotSubmitted;
            _inventory.ItemsProvider = VisibleItems;

            _detail.AddActionListener(ExecuteDetailAction);
            _eventsBound = true;
        }

        private void UnbindUIEvents()
        {
            if (!_eventsBound)
                return;

            for (int i = 0; i < _filterTabs.Length && i < _filterActions.Count; i++)
                _filterTabs[i].button.onClick.RemoveListener(_filterActions[i]);
            _filterActions.Clear();
            foreach (LoadoutAssignmentSlotView slot in _slots.QuickSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnQuickSlotClicked;
                slot.DoubleClicked -= OnQuickSlotDoubleClicked;
                slot.Dropped -= OnAssignmentSlotDropped;
            }
            foreach (LoadoutAssignmentSlotView slot in _slots.EquipmentSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnEquipmentSlotClicked;
                slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
                slot.Dropped -= OnAssignmentSlotDropped;
            }
            _dragDrop.Unbind();
            ItemSlotDragContext.Started -= OnItemDragStarted;
            ItemSlotDragContext.Ended -= OnItemDragEnded;

            _inventory.OnSlotClicked -= OnInventorySlotClicked;
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted -= OnInventorySlotSubmitted;
            _inventory.ItemsProvider = null;

            _detail.RemoveActionListener(ExecuteDetailAction);
            _eventsBound = false;
        }

        private void OnItemDragStarted(ItemSlotDragPayload payload)
        {
            _slots.ClearPreviews();
            _slots.ClearHighlight();
            _inventory.ClearSelection();
            RefreshSlotAvailability(payload.Stack?.Data);
        }

        private void OnItemDragEnded() => RefreshSlotAvailability(_selectedStack?.Data);

        private void BindDataEvents()
        {
            InventoryManager manager = InventoryManager.Instance;
            if (_subscribedInventoryManager != manager)
            {
                UnbindInventoryEvents();
                _subscribedInventoryManager = manager;
            }

            if (_subscribedInventoryManager != null)
            {
                _subscribedInventoryManager.Changed -= OnInventoryChanged;
                _subscribedInventoryManager.Changed += OnInventoryChanged;
            }

            var quickFood = InventoryManager.CurrentQuickFood;
            if (_subscribedQuickFood != quickFood)
            {
                if (_subscribedQuickFood != null)
                    _subscribedQuickFood.Changed -= OnQuickFoodChanged;
                _subscribedQuickFood = quickFood;
                if (_subscribedQuickFood != null)
                    _subscribedQuickFood.Changed += OnQuickFoodChanged;
            }

            var equipment = InventoryManager.CurrentEquipment;
            if (_subscribedEquipment != equipment)
            {
                if (_subscribedEquipment != null)
                    _subscribedEquipment.Changed -= OnEquipmentChanged;
                _subscribedEquipment = equipment;
                if (_subscribedEquipment != null)
                    _subscribedEquipment.Changed += OnEquipmentChanged;
            }
        }

        private void UnbindDataEvents()
        {
            UnbindInventoryEvents();
            if (_subscribedQuickFood != null)
                _subscribedQuickFood.Changed -= OnQuickFoodChanged;
            _subscribedQuickFood = null;

            if (_subscribedEquipment != null)
                _subscribedEquipment.Changed -= OnEquipmentChanged;
            _subscribedEquipment = null;
        }

        private void UnbindInventoryEvents()
        {
            if (_subscribedInventoryManager == null)
                return;

            _subscribedInventoryManager.Changed -= OnInventoryChanged;
            _subscribedInventoryManager = null;
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
            RefreshTabVisuals();
            RefreshInventory(ItemListView.ScrollRefreshMode.ScrollToTop);
            _slots.RefreshQuick();
            _slots.RefreshEquipment();
            RefreshTargetSelection();
            FocusInventoryOrSlots();
        }

        private void SelectFilter(int index)
        {
            if (index < 0 || index > (int)Filter.Equipment)
                return;

            _filter = (Filter)index;
            _selectedStack = null;
            RefreshTabVisuals();
            RefreshInventory(ItemListView.ScrollRefreshMode.ScrollToTop);
            RefreshTargetSelection();
            ShowEmptyDetail();
            FocusInventoryOrSlots();
        }

        private void FocusInventoryOrSlots()
        {
            if (_inventory.SelectFirstVisibleSlotAndClaimFocus())
                return;

            SlotKeyboardFocus.Claim(this);
            SelectFocusedAssignmentSlot(0);
        }

        private void Update()
        {
            if (!_initialized)
                return;

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.SubTabPrev)
                || UIInputActionMap.WasPressedThisFrame(UIInputActionMap.SubTabNext)
            )
            {
                SelectFilter(_filter == Filter.Quick ? (int)Filter.Equipment : (int)Filter.Quick);
                return;
            }

            if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.AuxAction))
            {
                if (SlotKeyboardFocus.IsFocused(this))
                {
                    if (!_inventory.SelectFirstVisibleSlotAndClaimFocus())
                        SelectFocusedAssignmentSlot(1);
                }
                else
                {
                    SlotKeyboardFocus.Claim(this);
                    SelectFocusedAssignmentSlot(0);
                }
                return;
            }

            if (!SlotKeyboardFocus.IsFocused(this))
                return;

            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
            {
                int delta = step.x != 0 ? step.x : -step.y;
                if (delta != 0)
                    SelectFocusedAssignmentSlot(delta > 0 ? 1 : -1);
            }
            else if (UIInputActionMap.WasSubmitPressedThisFrame())
            {
                ExecuteDetailAction();
            }
        }

        private void SelectFocusedAssignmentSlot(int offset)
        {
            var quickSlots = _slots.QuickSlots;
            var equipmentSlots = _slots.EquipmentSlots;
            if (_filter == Filter.Quick && quickSlots.Count > 0)
            {
                _slots.SelectedQuick =
                    (_slots.SelectedQuick + offset + quickSlots.Count) % quickSlots.Count;
                OnQuickSlotClicked(quickSlots[_slots.SelectedQuick]);
            }
            else if (_filter == Filter.Equipment && equipmentSlots.Count > 0)
            {
                _slots.SelectedEquipment =
                    (_slots.SelectedEquipment + offset + equipmentSlots.Count)
                    % equipmentSlots.Count;
                OnEquipmentSlotClicked(equipmentSlots[_slots.SelectedEquipment]);
            }
        }

        private void RefreshTabVisuals()
        {
            for (int i = 0; i < _filterTabs.Length; i++)
            {
                FilterTab tab = _filterTabs[i];
                bool selected = i == (int)_filter;
                if (tab.button.targetGraphic is Image background)
                    background.color = selected ? _tabSelectedColor : _tabNormalColor;
                tab.label.color = selected ? _tabSelectedTextColor : _tabNormalTextColor;
            }

            RefreshFilterCounts();
        }

        private void RefreshFilterCounts()
        {
            InventoryManager inventoryManager = ActiveInventoryManager;
            IReadOnlyList<ItemStack> items =
                inventoryManager != null ? inventoryManager.GetAllItems() : null;
            int quickCount = 0;
            int equipmentCount = 0;

            if (items != null)
            {
                foreach (ItemStack stack in items)
                {
                    switch (GetCategory(stack))
                    {
                        case ItemCategory.Food:
                            quickCount++;
                            break;
                        case ItemCategory.Equipment:
                            equipmentCount++;
                            break;
                    }
                }
            }

            int[] counts = { quickCount, equipmentCount };
            for (int i = 0; i < _filterTabs.Length && i < counts.Length; i++)
                _filterTabs[i].count.text = counts[i].ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshInventory(ItemListView.ScrollRefreshMode scrollMode) =>
            _inventory.SetItems(VisibleItems(), scrollMode);

        // 一覧に出すのは、選んでいるフィルタ(食材/装備)に合うアイテムだけ。
        private List<ItemStack> VisibleItems()
        {
            InventoryManager inventoryManager = ActiveInventoryManager;
            return inventoryManager != null
                ? inventoryManager.GetAllItems().Where(IsVisibleInCurrentFilter).ToList()
                : null;
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
                _ => false,
            };
        }

        private void OnInventorySlotClicked(ItemStack stack)
        {
            if (stack?.Data == null)
                return;

            if (
                _selectedStack == stack
                && _detail.CurrentAction == LoadoutDetailView.ButtonAction.Assign
            )
                return;

            _selectedStack = stack;
            ShowDetail(stack, LoadoutDetailView.ButtonAction.Assign);
            _slots.SelectTargetFor(stack.Data.category);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            OnInventorySlotClicked(stack);
            AssignSelectedStack();
        }

        private void OnInventorySlotSubmitted(ItemStack stack)
        {
            OnInventorySlotClicked(stack);
            ExecuteDetailAction();
        }

        private void OnQuickSlotClicked(LoadoutAssignmentSlotView slot) =>
            OnAssignmentSlotClicked(
                slot,
                ItemCategory.Food,
                LoadoutDetailView.ButtonAction.RemoveQuick
            );

        private void OnEquipmentSlotClicked(LoadoutAssignmentSlotView slot) =>
            OnAssignmentSlotClicked(
                slot,
                ItemCategory.Equipment,
                LoadoutDetailView.ButtonAction.RemoveEquipment
            );

        // 枠を選ぶ。中身があればその詳細(外すボタン)、空なら選んでいる同じ種類のアイテムの詳細(入れるボタン)を出す。
        private void OnAssignmentSlotClicked(
            LoadoutAssignmentSlotView slot,
            ItemCategory category,
            LoadoutDetailView.ButtonAction removeAction
        )
        {
            if (category == ItemCategory.Food)
                _slots.SelectedQuick = IndexOf(_slots.QuickSlots, slot);
            else
                _slots.SelectedEquipment = IndexOf(_slots.EquipmentSlots, slot);
            _slots.Highlight(category);

            if (slot.Stack != null)
            {
                if (_selectedStack == slot.Stack && _detail.CurrentAction == removeAction)
                    return;

                SelectStack(slot.Stack, removeAction);
            }
            else if (GetCategory(_selectedStack) == category)
            {
                if (_detail.CurrentAction == LoadoutDetailView.ButtonAction.Assign)
                    return;

                ShowDetail(_selectedStack, LoadoutDetailView.ButtonAction.Assign);
            }
            else
            {
                ShowEmptyDetail();
            }
        }

        private void OnQuickSlotDoubleClicked(LoadoutAssignmentSlotView slot)
        {
            int index = IndexOf(_slots.QuickSlots, slot);
            if (index < 0 || slot.Stack == null)
                return;

            _slots.SelectedQuick = index;
            RemoveQuickItem();
        }

        private void OnEquipmentSlotDoubleClicked(LoadoutAssignmentSlotView slot)
        {
            int index = IndexOf(_slots.EquipmentSlots, slot);
            if (index < 0 || slot.Stack == null)
                return;

            _slots.SelectedEquipment = index;
            RemoveEquipment();
        }

        private void OnAssignmentSlotDropped(
            LoadoutAssignmentSlotView target,
            ItemSlotDragPayload payload
        )
        {
            int quickTarget = IndexOf(_slots.QuickSlots, target);
            if (quickTarget >= 0)
            {
                DropIntoQuickSlot(quickTarget, payload);
                return;
            }

            int equipmentTarget = IndexOf(_slots.EquipmentSlots, target);
            if (equipmentTarget >= 0)
                DropIntoEquipmentSlot(equipmentTarget, payload);
        }

        private void DropIntoQuickSlot(int targetIndex, ItemSlotDragPayload payload)
        {
            if (GetCategory(payload.Stack) != ItemCategory.Food)
                return;

            _slots.SelectedQuick = targetIndex;
            _slots.Highlight(ItemCategory.Food);

            if (payload.Origin == ItemSlotDragOrigin.Quick)
            {
                if (_slots.SwapQuick(payload.SlotIndex, targetIndex))
                    SelectStack(payload.Stack, LoadoutDetailView.ButtonAction.RemoveQuick);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignQuickItem();
            _inventory.SelectItem(payload.Stack);
        }

        private void DropIntoEquipmentSlot(int targetIndex, ItemSlotDragPayload payload)
        {
            if (GetCategory(payload.Stack) != ItemCategory.Equipment)
                return;

            _slots.SelectedEquipment = targetIndex;
            _slots.Highlight(ItemCategory.Equipment);

            if (payload.Origin == ItemSlotDragOrigin.Equipment)
            {
                if (_slots.SwapEquipment(payload.SlotIndex, targetIndex))
                    SelectStack(payload.Stack, LoadoutDetailView.ButtonAction.RemoveEquipment);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignEquipment();
            _inventory.SelectItem(payload.Stack);
        }

        // アイテムを選んだことにして、一覧でも選び、詳細を出す。
        private void SelectStack(ItemStack stack, LoadoutDetailView.ButtonAction action)
        {
            _selectedStack = stack;
            _inventory.SelectItem(stack);
            ShowDetail(stack, action);
        }

        private void RefreshTargetSelection() =>
            _slots.Highlight(
                GetCategory(_selectedStack) == ItemCategory.Equipment
                    ? ItemCategory.Equipment
                    : ItemCategory.Food
            );

        private void ExecuteDetailAction()
        {
            switch (_detail.CurrentAction)
            {
                case LoadoutDetailView.ButtonAction.Assign:
                    AssignSelectedStack();
                    break;
                case LoadoutDetailView.ButtonAction.RemoveQuick:
                    RemoveQuickItem();
                    break;
                case LoadoutDetailView.ButtonAction.RemoveEquipment:
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
            if (_slots.AssignQuick(_selectedStack))
                ShowDetail(_selectedStack, LoadoutDetailView.ButtonAction.RemoveQuick);
        }

        private void AssignEquipment()
        {
            if (!_slots.AssignEquipment(_selectedStack))
                return;

            ShowDetail(_selectedStack, LoadoutDetailView.ButtonAction.RemoveEquipment);
        }

        private void RemoveQuickItem()
        {
            if (!_slots.RemoveQuick())
                return;

            ClearSelectedStack();
            ShowEmptyDetail();
        }

        private void RemoveEquipment()
        {
            if (!_slots.RemoveEquipment())
                return;

            ClearSelectedStack();
            RefreshInventory(ItemListView.ScrollRefreshMode.KeepPosition);
            ShowEmptyDetail();
        }

        private void ClearSelectedStack()
        {
            _selectedStack = null;
            _inventory.ClearSelection();
        }

        private void ShowDetail(ItemStack stack, LoadoutDetailView.ButtonAction action)
        {
            ItemData item = stack?.Data;
            if (item == null)
            {
                ShowEmptyDetail();
                return;
            }

            _detail.Show(stack, action);
            RefreshSlotAvailability(item);
        }

        private void ShowEmptyDetail(bool replayText = false)
        {
            _detail.ShowEmpty(replayText);
            RefreshSlotAvailability(null);
        }

        // アイテムを選んでいればその種類の枠、選んでいなければ今の絞り込みの枠を明るくする。
        private void RefreshSlotAvailability(ItemData item)
        {
            if (item != null)
                _slots.SetAvailable(
                    item.category == ItemCategory.Food,
                    item.category == ItemCategory.Equipment
                );
            else
                _slots.SetAvailable(_filter != Filter.Equipment, _filter != Filter.Quick);
        }

        private static int IndexOf(
            IReadOnlyList<LoadoutAssignmentSlotView> slots,
            LoadoutAssignmentSlotView slot
        )
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == slot)
                    return i;
            return -1;
        }

        private void OnInventoryChanged()
        {
            RefreshTabVisuals();
            RefreshInventory(ItemListView.ScrollRefreshMode.KeepPosition);
            _slots.RefreshQuick();
            _slots.RefreshEquipment();
        }

        private void OnQuickFoodChanged() => _slots.RefreshQuick();

        private void OnEquipmentChanged()
        {
            _slots.RefreshEquipment();
            RefreshInventory(ItemListView.ScrollRefreshMode.KeepPosition);
        }
    }
}
