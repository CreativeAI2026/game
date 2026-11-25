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
    public sealed class LoadoutPanelController : MonoBehaviour
    {
        private enum Filter
        {
            Quick,
            Equipment,
        }

        [SerializeField]
        private Color _tabNormalColor = new(0f, 0f, 0f, 0.39f);

        [SerializeField]
        private Color _tabSelectedColor = new(0.31f, 0.23f, 0.12f, 0.9f);

        [SerializeField]
        private Color _tabNormalTextColor = Color.white;

        [SerializeField]
        private Color _tabSelectedTextColor = new Color32(0xff, 0xc3, 0x46, 0xff);

#pragma warning disable CS0414 // エディタ専用(#if UNITY_EDITOR)でのみ読む。プレイヤービルドでは未使用になる
        [Header("開発用プレビュー")]
        [SerializeField]
        [Tooltip("所持品が空の開発用シーンでは、確認用の所持品を生成します。")]
        private bool _seedPreviewInventoryWhenEmpty = true;
#pragma warning restore CS0414

        [Header("詳細テキストアニメーション")]
        [SerializeField]
        private bool _animateDetailText = true;

        [SerializeField, Min(0f)]
        private float _detailTextStartDelay = 0.05f;

        [SerializeField, Min(0.001f)]
        private float _detailCharacterInterval = 0.035f;

        [SerializeField, Min(0f)]
        private float _detailSectionDelay = 0.06f;

        [Header("Slot Appearance")]
        [SerializeField, Min(0f)]
        private float _slotAppearDuration = 0.2f;

        [SerializeField, Min(0f)]
        private float _slotAppearStagger = 0.05f;

        private readonly List<Button> _filterButtons = new();
        private readonly List<TMP_Text> _filterLabels = new();
        private readonly List<TMP_Text> _filterCountLabels = new();
        private readonly List<UnityAction> _filterActions = new();

        private ItemListView _inventory;
        private TMP_Text _equipmentTitle;
        private LoadoutDropErrorFeedback _dropErrorFeedback;
        private LoadoutDetailPanel _detail;
        private LoadoutAssignment _slots;
        private LoadoutDragDrop _dragDrop;

        private Filter _filter;
        private ItemStack _selectedStack;
        private bool _initialized;
        private bool _eventsBound;
        private Coroutine _refreshRoutine;
        private InventoryManager _subscribedInventoryManager;

        private InventoryManager ActiveInventoryManager =>
            _subscribedInventoryManager != null
                ? _subscribedInventoryManager
                : InventoryManager.Instance;

        private void Awake()
        {
            _detail = new LoadoutDetailPanel(
                this,
                new LoadoutDetailPanel.TextTiming(
                    _animateDetailText,
                    _detailTextStartDelay,
                    _detailCharacterInterval,
                    _detailSectionDelay
                )
            );
            _slots = new LoadoutAssignment(() => ActiveInventoryManager);
            ResolveReferences();
            _dragDrop = new LoadoutDragDrop(
                _slots,
                _dropErrorFeedback,
                OnItemDragStarted,
                OnItemDragEnded,
                OnAssignmentSlotDropped
            );
            Initialize();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (!Initialize())
                return;

            _selectedStack = null;
            if (_inventory != null)
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

        private bool Initialize()
        {
            if (_initialized)
                return true;

            ResolveFilterButtons();
            _slots.Resolve(transform);
            if (!ValidateRequiredReferences())
                return false;

            BindUIEvents();

            if (_inventory != null)
                _inventory.SetSelectFirstSlotOnRefresh(false);
            if (_inventory != null)
                _inventory.SetShowItemCounts(true);
            if (_equipmentTitle != null)
                _equipmentTitle.text = "装備";

            _slots.SelectedQuick = 0;
            _slots.SelectedEquipment = 0;
            _filter = Filter.Quick;
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
                    $"{nameof(LoadoutPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}' に ItemListView がありません。InventoryArea 以下へ配置してください。",
                    this
                );
                enabled = false;
                return false;
            }

            var warnings = new List<string>();
            if (_filterButtons.Count != 2)
                warnings.Add($"カテゴリボタン2個（現在 {_filterButtons.Count} 個）");
            _slots.CollectMissing(warnings);
            _detail.CollectMissing(warnings);

            if (warnings.Count > 0)
                Debug.LogWarning(
                    $"{nameof(LoadoutPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}' の補助UIが一部不足しています。一覧表示は続行します: {string.Join(", ", warnings)}",
                    this
                );

            return true;
        }

        // 部品は画面の決まった場所から探す。見つかっていないものだけ探し直す。
        private void ResolveReferences()
        {
            _detail.Resolve();
            if (_inventory == null)
            {
                Transform scrollViewArea = transform.Find("InventoryArea/ScrollViewArea");
                _inventory =
                    scrollViewArea != null
                        ? scrollViewArea.GetComponentInChildren<ItemListView>(true)
                        : null;
            }
            if (_equipmentTitle == null)
                _equipmentTitle = FindComponent<TMP_Text>(transform, "EquipArea/TitleArea/Title");
            if (_dropErrorFeedback == null)
                _dropErrorFeedback = FindComponent<LoadoutDropErrorFeedback>(
                    transform,
                    "DropErrorMessage"
                );
        }

        private static T FindComponent<T>(Transform root, string path)
            where T : Component
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private static ItemCategory? GetCategory(ItemStack stack)
        {
            ItemData data = stack?.Data;
            return data != null ? data.category : null;
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
                if (button == null)
                    continue;

                _filterButtons.Add(button);
                _filterLabels.Add(FindComponent<TMP_Text>(button.transform, "Label"));
                _filterCountLabels.Add(FindComponent<TMP_Text>(button.transform, "Image/Number"));
            }
        }

        private void BindUIEvents()
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

            foreach (LoadoutAssignmentSlotView slot in _slots.QuickSlots)
            {
                slot.Clicked += OnQuickSlotClicked;
                slot.DoubleClicked += OnQuickSlotDoubleClicked;
            }
            foreach (LoadoutAssignmentSlotView slot in _slots.EquipmentSlots)
            {
                slot.Clicked += OnEquipmentSlotClicked;
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;
            }
            _dragDrop.Bind();

            if (_inventory != null)
            {
                _inventory.OnSlotClicked += OnInventorySlotClicked;
                _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
                _inventory.OnSlotSubmitted += OnInventorySlotSubmitted;
                _inventory.DisplayRefreshRequested += OnInventoryRefreshRequested;
            }

            _detail.AddActionListener(ExecuteDetailAction);
            _eventsBound = true;
        }

        private void UnbindUIEvents()
        {
            if (!_eventsBound)
                return;

            for (int i = 0; i < _filterButtons.Count && i < _filterActions.Count; i++)
                if (_filterButtons[i] != null)
                    _filterButtons[i].onClick.RemoveListener(_filterActions[i]);
            _filterActions.Clear();
            foreach (LoadoutAssignmentSlotView slot in _slots.QuickSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnQuickSlotClicked;
                slot.DoubleClicked -= OnQuickSlotDoubleClicked;
            }
            foreach (LoadoutAssignmentSlotView slot in _slots.EquipmentSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnEquipmentSlotClicked;
                slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
            }
            _dragDrop.Unbind();

            if (_inventory != null)
            {
                _inventory.OnSlotClicked -= OnInventorySlotClicked;
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
                _inventory.OnSlotSubmitted -= OnInventorySlotSubmitted;
                _inventory.DisplayRefreshRequested -= OnInventoryRefreshRequested;
            }

            _detail.RemoveActionListener(ExecuteDetailAction);
            _eventsBound = false;
        }

        private void OnItemDragStarted(ItemSlotDragPayload payload)
        {
            if (_inventory != null)
                _inventory.ClearSelection();
            RefreshSlotAvailability(payload.Stack?.Data);
        }

        private void OnItemDragEnded() => RefreshSlotAvailability(_selectedStack?.Data);

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
                if (manager == null)
                    manager = InventoryManager.EnsureResident();
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
            RefreshTabVisuals();
            RefreshInventory(ItemListView.ScrollRefreshMode.ScrollToTop);
            _slots.RefreshQuick();
            _slots.SyncEquipment();
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
            if (_inventory != null && _inventory.SelectFirstVisibleSlotAndClaimFocus())
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
                    if (_inventory == null || !_inventory.SelectFirstVisibleSlotAndClaimFocus())
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
            for (int i = 0; i < _filterButtons.Count; i++)
            {
                Button button = _filterButtons[i];
                bool selected = i == (int)_filter;
                if (button.targetGraphic is Image background)
                    background.color = selected ? _tabSelectedColor : _tabNormalColor;

                TMP_Text label = i < _filterLabels.Count ? _filterLabels[i] : null;
                if (label != null)
                    label.color = selected ? _tabSelectedTextColor : _tabNormalTextColor;
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
            for (int i = 0; i < _filterCountLabels.Count && i < counts.Length; i++)
            {
                if (_filterCountLabels[i] != null)
                    _filterCountLabels[i].text = counts[i].ToString(CultureInfo.InvariantCulture);
            }
        }

        private void OnInventoryRefreshRequested(
            TabDefinition _definition,
            int _tabIndex,
            ItemListView.ScrollRefreshMode scrollMode
        ) => RefreshInventory(scrollMode);

        private void RefreshInventory(ItemListView.ScrollRefreshMode scrollMode)
        {
            if (_inventory == null)
                return;

            InventoryManager inventoryManager = ActiveInventoryManager;
            List<ItemStack> items =
                inventoryManager != null ? inventoryManager.GetAllItems() : null;
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
                _ => false,
            };
        }

        private void OnInventorySlotClicked(ItemStack stack)
        {
            if (stack?.Data == null)
                return;

            if (
                _selectedStack == stack
                && _detail.CurrentAction == LoadoutDetailPanel.ButtonAction.Assign
            )
                return;

            _selectedStack = stack;
            ShowDetail(stack, LoadoutDetailPanel.ButtonAction.Assign);
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
                LoadoutDetailPanel.ButtonAction.RemoveQuick
            );

        private void OnEquipmentSlotClicked(LoadoutAssignmentSlotView slot) =>
            OnAssignmentSlotClicked(
                slot,
                ItemCategory.Equipment,
                LoadoutDetailPanel.ButtonAction.RemoveEquipment
            );

        // 枠を選ぶ。中身があればその詳細(外すボタン)、空なら選んでいる同じ種類のアイテムの詳細(入れるボタン)を出す。
        private void OnAssignmentSlotClicked(
            LoadoutAssignmentSlotView slot,
            ItemCategory category,
            LoadoutDetailPanel.ButtonAction removeAction
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
                if (_detail.CurrentAction == LoadoutDetailPanel.ButtonAction.Assign)
                    return;

                ShowDetail(_selectedStack, LoadoutDetailPanel.ButtonAction.Assign);
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
                    SelectStack(payload.Stack, LoadoutDetailPanel.ButtonAction.RemoveQuick);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignQuickItem();
            if (_inventory != null)
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
                    SelectStack(payload.Stack, LoadoutDetailPanel.ButtonAction.RemoveEquipment);
                return;
            }

            if (payload.Origin != ItemSlotDragOrigin.Inventory)
                return;

            _selectedStack = payload.Stack;
            AssignEquipment();
            if (_inventory != null)
                _inventory.SelectItem(payload.Stack);
        }

        // アイテムを選んだことにして、一覧でも選び、詳細を出す。
        private void SelectStack(ItemStack stack, LoadoutDetailPanel.ButtonAction action)
        {
            _selectedStack = stack;
            if (_inventory != null)
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
                case LoadoutDetailPanel.ButtonAction.Assign:
                    AssignSelectedStack();
                    break;
                case LoadoutDetailPanel.ButtonAction.RemoveQuick:
                    RemoveQuickItem();
                    break;
                case LoadoutDetailPanel.ButtonAction.RemoveEquipment:
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
                ShowDetail(_selectedStack, LoadoutDetailPanel.ButtonAction.RemoveQuick);
        }

        private void AssignEquipment()
        {
            if (!_slots.AssignEquipment(_selectedStack))
                return;

            RefreshInventory(ItemListView.ScrollRefreshMode.KeepPosition);
            ShowDetail(_selectedStack, LoadoutDetailPanel.ButtonAction.RemoveEquipment);
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
            if (_inventory != null)
                _inventory.ClearSelection();
        }

        private void ShowDetail(ItemStack stack, LoadoutDetailPanel.ButtonAction action)
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
            _slots.SyncEquipment();
            _slots.RefreshEquipment();
        }

        private void OnQuickFoodChanged() => _slots.RefreshQuick();

        private void OnEquipmentChanged()
        {
            if (_slots.IsChangingEquipment)
                return;

            _slots.SyncEquipment();
            _slots.RefreshEquipment();
            RefreshInventory(ItemListView.ScrollRefreshMode.KeepPosition);
        }
    }
}
