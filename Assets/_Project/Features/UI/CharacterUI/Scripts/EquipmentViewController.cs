using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.UI
{
    public class EquipmentViewController : MonoBehaviour, ICharacterTabView
    {
        private enum AssignmentMode
        {
            Equipment,
            QuickConsumable,
        }

        private TriangleLayout _triangleLayout;

        [Header("Equipment Slots Root")]
        [SerializeField]
        private Transform _equipmentSlotsRoot;

        [Header("Detail Panel")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Inventory")]
        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private ItemCategory _inventoryCategory = ItemCategory.Equipment;

        [SerializeField]
        private AssignmentMode _assignmentMode = AssignmentMode.Equipment;

        [SerializeField]
        private string _emptyLabel = "\uFF08\u672A\u88C5\u5099\uFF09";

        private readonly List<EquipmentSlot> _slots = new();
        private int _currentSlotIndex;
        private ItemStack _selectedInventoryStack;
        private bool _initialized;
        private bool _subscribedToInventoryChanged;
        private bool _subscribedToQuickFoodChanged;
        private bool _warnedMissingInventory;
        private bool _warnedMissingDetailPanel;

        private bool HasSlots => _slots.Count > 0;

        private EquipmentSlot CurrentSlot =>
            HasSlots && _currentSlotIndex >= 0 && _currentSlotIndex < _slots.Count
                ? _slots[_currentSlotIndex]
                : null;

        private void Awake()
        {
            if (!ValidateRequiredReferences())
                return;

            ResolveConfiguredComponents();
            BindInventoryItemsRequested();
            ConfigureInventory();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_initialized)
                return;

            if (!ValidateRequiredReferences())
                return;

            ResolveConfiguredComponents();
            BindInventoryItemsRequested();
            ConfigureInventory();
            InitializeSlots();
            InitializeAssignedItems();
            BindInventoryEvents();
            BindInventoryChangedEvent();
            BindQuickFoodChangedEvent();

            RefreshSlotLayout();
            SelectEquipmentSlot(0);

            _initialized = true;
            BindTriangleLayoutEvents();
        }

        private void OnEnable()
        {
            if (!ValidateRequiredReferences())
                return;

            BindInventoryItemsRequested();

            if (_initialized)
            {
                BindInventoryChangedEvent();
                BindQuickFoodChangedEvent();
            }
        }

        private void OnDisable()
        {
            UnbindInventoryItemsRequested();
            UnbindInventoryChangedEvent();
            UnbindQuickFoodChangedEvent();
        }

        private void OnDestroy()
        {
            UnbindSlots();
            UnbindInventoryEvents();
            UnbindInventoryItemsRequested();
            UnbindInventoryChangedEvent();
            UnbindQuickFoodChangedEvent();
            if (_triangleLayout != null)
                _triangleLayout.AnimationStateChanged -= SetSlotsInputLocked;
        }

        public void Configure(ItemCategory inventoryCategory, string emptyLabel)
        {
            _inventoryCategory = inventoryCategory;
            _emptyLabel = emptyLabel;
            if (!ValidateRequiredReferences())
                return;

            ResolveConfiguredComponents();
            BindInventoryItemsRequested();
            ConfigureInventory();
        }

        public void OnEnter()
        {
            EnsureInitialized();

            if (!HasSlots)
                return;

            RefreshAssignedSlots();
            SelectEquipmentSlot(GetTopEquipmentSlotIndex());
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            _selectedInventoryStack = null;
            RefreshDetailFromCurrentSlot();
        }

        public void OnExit()
        {
            if (!HasSlots)
                return;

            _detailPanel?.Clear();
            _selectedInventoryStack = null;
            _inventory?.ClearSelection();
        }

        public void ResetViewState()
        {
            EnsureInitialized();

            _selectedInventoryStack = null;
            _inventory?.ResetViewState();

            if (!HasSlots)
            {
                _detailPanel?.Clear();
                return;
            }

            SelectEquipmentSlot(0);
            RotateSlotToTop(0);
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            RefreshDetailFromCurrentSlot();
        }

        private void ResolveConfiguredComponents()
        {
            if (_triangleLayout == null && _equipmentSlotsRoot != null)
                _triangleLayout = _equipmentSlotsRoot.GetComponent<TriangleLayout>();
        }

        private bool ValidateRequiredReferences()
        {
            bool valid = true;
            if (_inventory == null)
            {
                WarnMissingReferenceOnce(ref _warnedMissingInventory, nameof(_inventory));
                valid = false;
            }
            if (_detailPanel == null)
            {
                WarnMissingReferenceOnce(ref _warnedMissingDetailPanel, nameof(_detailPanel));
                valid = false;
            }

            return valid;
        }

        private void WarnMissingReferenceOnce(ref bool warned, string fieldName)
        {
            if (warned)
                return;

            warned = true;
            Debug.LogWarning(
                $"{nameof(EquipmentViewController)} '{CreativeAI.UI.UIHierarchyPathUtility.GetPath(transform)}' requires Inspector reference '{fieldName}'. Initialization was stopped.",
                this
            );
        }

        private void ConfigureInventory()
        {
            _inventory?.SetSelectFirstSlotOnRefresh(false);
            _inventory?.SetShowItemCounts(_assignmentMode == AssignmentMode.QuickConsumable);
        }

        private bool IsSlotInputLocked()
        {
            return _triangleLayout != null && _triangleLayout.IsAnimating;
        }

        private void BindTriangleLayoutEvents()
        {
            if (_triangleLayout == null)
                return;

            _triangleLayout.AnimationStateChanged -= SetSlotsInputLocked;
            _triangleLayout.AnimationStateChanged += SetSlotsInputLocked;
        }

        private void SetSlotsInputLocked(bool locked)
        {
            foreach (var slot in _slots)
                slot?.SetInputLocked(locked);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _detailPanel ??= GetComponentInChildren<ItemDetailPanel>(true);
            _inventory ??= GetComponentInChildren<InventoryView>(true);
        }
#endif

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotClicked -= OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotClicked += OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotClicked -= OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
        }

        private void BindInventoryItemsRequested()
        {
            if (_inventory == null)
                return;

            _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnInventoryItemsRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
        }

        private void UnbindInventoryItemsRequested()
        {
            if (_inventory != null)
            {
                _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
                _inventory.ItemsRequested -= OnInventoryItemsRequested;
            }
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition _definition,
            int _tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            _inventory?.RequestItems(_inventoryCategory, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null || category != _inventoryCategory)
                return;

            var items = InventoryManager.Instance?.GetItemsByCategory(_inventoryCategory);
            _inventory.SetItems(items, scrollMode);
        }

        private void BindInventoryChangedEvent()
        {
            if (_subscribedToInventoryChanged || InventoryManager.Instance == null)
                return;

            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
            _subscribedToInventoryChanged = true;
        }

        private void UnbindInventoryChangedEvent()
        {
            if (!_subscribedToInventoryChanged)
                return;

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;

            _subscribedToInventoryChanged = false;
        }

        private void OnInventoryChanged()
        {
            _inventory?.RefreshCurrentTab();
            if (_assignmentMode == AssignmentMode.QuickConsumable)
                RefreshQuickConsumableCounts();
            else
                SyncEquipmentSlotsWithInventory();
        }

        private void SyncInventorySelection(ItemStack stack)
        {
            _inventory?.SelectItem(stack);
        }

        private void OnInventorySlotSelected(ItemStack stack)
        {
            if (!IsValidStack(stack))
                return;

            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (!IsValidStack(stack) || !HasSlots)
                return;

            if (_assignmentMode == AssignmentMode.QuickConsumable)
            {
                AssignQuickConsumable(stack);
                return;
            }

            int equippedSlotIndex = _slots.FindIndex(slot => slot.Stack == stack);
            if (stack.IsEquipped || equippedSlotIndex >= 0)
            {
                if (equippedSlotIndex >= 0)
                    SelectAndRotateSlot(equippedSlotIndex);

                _selectedInventoryStack = stack;
                UnequipCurrentSlot();
                return;
            }

            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack);
            EquipSelectedItem();
        }

        private bool IsValidStack(ItemStack stack)
        {
            return stack?.Data != null && stack.Data.category == _inventoryCategory;
        }

        private void AssignQuickConsumable(ItemStack stack)
        {
            int targetSlotIndex = FirstEmptySlotIndex();
            if (targetSlotIndex < 0)
                targetSlotIndex = _currentSlotIndex;

            if (InventoryManager.Instance?.SetQuickFood(targetSlotIndex, stack) != true)
                return;

            SelectAndRotateSlot(targetSlotIndex);
            _detailPanel?.Show(stack);
        }

        private void EquipSelectedItem()
        {
            if (_selectedInventoryStack == null || CurrentSlot == null)
                return;

            if (_slots.Any(slot => slot != CurrentSlot && slot.Stack == _selectedInventoryStack))
                return;

            UnequipStack(CurrentSlot.Stack, false);
            CurrentSlot.EquipAnimated(_selectedInventoryStack);
            InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, true);

            _inventory?.UpdateItemEquippedState(_selectedInventoryStack, true, true);
            _detailPanel?.Show(_selectedInventoryStack);

            SelectNextEmptySlot();
            RefreshDetailFromCurrentSlot();
        }

        private void UnequipCurrentSlot()
        {
            if (CurrentSlot == null)
                return;

            if (_selectedInventoryStack != null && _selectedInventoryStack.IsEquipped)
            {
                UnequipStack(_selectedInventoryStack, true);
                ClearSlotWithStack(_selectedInventoryStack);
                RefreshDetailFromCurrentSlot();
                return;
            }

            var currentStack = CurrentSlot.Stack;
            if (currentStack == null)
                return;

            UnequipStack(currentStack, false);
            CurrentSlot.ClearAnimated();
            RefreshDetailFromCurrentSlot();
        }

        private void UnequipStack(ItemStack stack, bool keepSelected)
        {
            InventoryManager.Instance?.SetEquipped(stack, false);
            _inventory?.UpdateItemEquippedState(stack, false, keepSelected);
        }

        private void ClearSlotWithStack(ItemStack stack)
        {
            var slot = _slots.FirstOrDefault(candidate => candidate.Stack == stack);
            slot?.ClearAnimated();
        }

        private void SyncEquipmentSlotsWithInventory()
        {
            if (!HasSlots || InventoryManager.Instance == null)
                return;

            bool currentSlotChanged = false;
            bool selectedStackRemoved = false;

            foreach (var slot in _slots)
            {
                var stack = slot?.Stack;
                if (stack == null)
                    continue;

                if (InventoryManager.Instance.InventoryService.ContainsStack(stack))
                {
                    slot.UpdateCount();
                    continue;
                }

                if (_selectedInventoryStack == stack)
                    selectedStackRemoved = true;
                if (slot == CurrentSlot)
                    currentSlotChanged = true;

                InventoryManager.Instance.SetEquipped(stack, false);
                slot.Clear();
            }

            if (selectedStackRemoved)
                _selectedInventoryStack = null;

            if (currentSlotChanged)
            {
                SyncInventorySelection(CurrentSlot?.Stack);
                RefreshDetailFromCurrentSlot();
            }
        }

        private void Update()
        {
            if (
                !isActiveAndEnabled
                || !HasSlots
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectEquipmentSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectEquipmentSlotByOffset(1);
        }

        private void SelectEquipmentSlotByOffset(int offset)
        {
            if (IsSlotInputLocked())
                return;

            int nextIndex = (_currentSlotIndex + offset + _slots.Count) % _slots.Count;
            SelectAndRotateSlot(nextIndex);
        }

        private readonly HashSet<GameObject> _warnedMissingEquipmentSlots = new();

        private void InitializeSlots()
        {
            UnbindSlots();
            _slots.Clear();

            if (_equipmentSlotsRoot == null)
                return;

            for (int i = 0; i < _equipmentSlotsRoot.childCount; i++)
            {
                var slot = GetEquipmentSlot(_equipmentSlotsRoot.GetChild(i).gameObject);
                if (slot == null)
                    continue;

                slot.Init();
                slot.Clear();
                slot.Clicked += OnEquipmentSlotClicked;
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;

                _slots.Add(slot);
            }
        }

        private EquipmentSlot GetEquipmentSlot(GameObject slotObject)
        {
            var slot = slotObject.GetComponent<EquipmentSlot>();
            if (slot != null)
                return slot;

            if (_warnedMissingEquipmentSlots.Add(slotObject))
            {
                Debug.LogWarning(
                    $"{nameof(EquipmentViewController)}: Slot '{slotObject.name}' に {nameof(EquipmentSlot)} がないため、このスロットをスキップしました。PrefabまたはScene上で追加してください。",
                    slotObject
                );
            }

            return null;
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnEquipmentSlotClicked;
                slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
            }
        }

        private void EquipInitialTestItems()
        {
            var initialItems = InventoryManager
                .Instance?.GetAllItems()
                .Where(stack => stack.Data.category == _inventoryCategory && stack.IsEquipped)
                .Take(Mathf.Min(2, _slots.Count))
                .ToList();

            if (initialItems == null)
                return;

            foreach (var stack in initialItems)
            {
                int slotIndex = initialItems.IndexOf(stack);
                _slots[slotIndex].SetStack(stack);
                InventoryManager.Instance?.SetEquipped(stack, true);
            }
        }

        private void InitializeAssignedItems()
        {
            if (_assignmentMode == AssignmentMode.QuickConsumable)
                RefreshQuickConsumableSlots();
            else
                EquipInitialTestItems();
        }

        private void RefreshAssignedSlots()
        {
            if (_assignmentMode == AssignmentMode.QuickConsumable)
                RefreshQuickConsumableSlots();
        }

        private void RefreshQuickConsumableSlots()
        {
            if (!HasSlots)
                return;

            var assignedItems = InventoryManager.Instance?.GetQuickFoodSlots();
            for (int i = 0; i < _slots.Count; i++)
            {
                var stack =
                    assignedItems != null && i < assignedItems.Count ? assignedItems[i] : null;
                if (stack != null)
                    _slots[i].SetStack(stack);
                else
                    _slots[i].Clear();
            }
        }

        private void RefreshQuickConsumableCounts()
        {
            foreach (var slot in _slots)
                if (slot?.Stack != null)
                    slot.UpdateCount();
        }

        private int FirstEmptySlotIndex()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Stack == null)
                    return i;

            return -1;
        }

        private void SelectEquipmentSlot(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return;

            var previousSlot = CurrentSlot;
            var selectedSlot = _slots[index];

            previousSlot?.SetSelected(false);

            foreach (var slot in _slots)
                if (slot != null && slot != previousSlot && slot != selectedSlot)
                    slot.SetSelected(false);

            _currentSlotIndex = index;
            selectedSlot.SetSelected(true);

            _selectedInventoryStack = null;
            SyncInventorySelection(CurrentSlot.Stack);

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != selectedSlot
                && previousSlot.Stack == null
                && selectedSlot.Stack == null;
            _detailPanel?.Show(selectedSlot.Stack, _emptyLabel, changedBetweenEmptySlots);
        }

        private void OnEquipmentSlotDoubleClicked(EquipmentSlot slot)
        {
            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectEquipmentSlot(slotIndex);
            _selectedInventoryStack = null;

            if (_assignmentMode == AssignmentMode.QuickConsumable)
            {
                InventoryManager.Instance?.ClearQuickFood(slotIndex);
                return;
            }

            UnequipCurrentSlot();
        }

        private void OnEquipmentSlotClicked(EquipmentSlot slot)
        {
            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectEquipmentSlot(slotIndex);
        }

        private void SelectNextEmptySlot()
        {
            if (_slots.Count <= 1)
                return;

            int equippedSlotIndex = _currentSlotIndex;
            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (equippedSlotIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Stack != null)
                    continue;

                SelectAndRotateSlot(slotIndex);
                return;
            }
        }

        private void SelectAndRotateSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            SelectEquipmentSlot(slotIndex);
            RotateSlotToTop(slotIndex);
        }

        private int GetTopEquipmentSlotIndex()
        {
            if (_triangleLayout == null)
                return 0;

            int slotIndex = _triangleLayout.GetTopSlotIndex();
            return slotIndex >= 0 && slotIndex < _slots.Count ? slotIndex : 0;
        }

        private void RotateSlotToTop(int slotIndex)
        {
            _equipmentSlotsRoot?.GetComponent<TriangleLayout>()?.RotateSlotToTop(slotIndex);
        }

        private void RefreshSlotLayout()
        {
            _equipmentSlotsRoot?.GetComponent<TriangleLayout>()?.RefreshLayout();
        }

        private void RefreshDetailFromCurrentSlot()
        {
            if (_detailPanel == null || CurrentSlot == null)
                return;

            _detailPanel.Show(CurrentSlot.Stack, _emptyLabel);
        }

        private void BindQuickFoodChangedEvent()
        {
            if (
                _assignmentMode != AssignmentMode.QuickConsumable
                || _subscribedToQuickFoodChanged
                || InventoryManager.Instance == null
            )
                return;

            InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;
            InventoryManager.Instance.QuickFoodChanged += OnQuickFoodChanged;
            _subscribedToQuickFoodChanged = true;
        }

        private void UnbindQuickFoodChangedEvent()
        {
            if (!_subscribedToQuickFoodChanged)
                return;

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;

            _subscribedToQuickFoodChanged = false;
        }

        private void OnQuickFoodChanged()
        {
            RefreshQuickConsumableSlots();
            RefreshDetailFromCurrentSlot();
        }
    }
}
