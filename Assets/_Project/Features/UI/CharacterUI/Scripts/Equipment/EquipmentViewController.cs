using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController : MonoBehaviour, ICharacterTabView
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
    }
}
