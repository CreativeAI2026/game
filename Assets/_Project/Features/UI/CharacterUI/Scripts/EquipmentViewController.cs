using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public partial class EquipmentViewController : MonoBehaviour
    {
        private static readonly Color SlotFrameSelected = new(1f, 0.78f, 0.15f, 0.9f);
        private static readonly Color SlotFrameNormal = new(1f, 1f, 1f, 0.15f);

        private TriangleLayout _triangleLayout;

        [Header("Equipment Slots")]
        [SerializeField]
        private Transform _equipmentSlotsContainer;

        [Header("Detail")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Inventory")]
        [SerializeField]
        private Inventory _inventory;

        [SerializeField]
        private ItemCategory _inventoryCategory = ItemCategory.Equipment;

        [SerializeField]
        private string _emptyLabel = "\uFF08\u672A\u88C5\u5099\uFF09";

        private readonly List<EquipmentSlot> _slots = new();
        private readonly Dictionary<Button, UnityAction> _slotButtonActions = new();
        private int _currentSlotIndex;
        private ItemStack _selectedInventoryStack;
        private bool _initialized;

        private bool HasSlots => _slots.Count > 0;

        private EquipmentSlot CurrentSlot =>
            HasSlots && _currentSlotIndex >= 0 && _currentSlotIndex < _slots.Count
                ? _slots[_currentSlotIndex]
                : null;

        private void Awake()
        {
            ResolveReferences();
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

            ResolveReferences();
            ConfigureInventory();
            InitializeSlots();
            EquipInitialTestItems();
            BindInventoryEvents();

            RefreshSlotLayout();
            SelectEquipmentSlot(0);

            _initialized = true;
            BindTriangleLayoutEvents();
        }

        private void OnDestroy()
        {
            UnbindSlots();
            UnbindInventoryEvents();
            if (_triangleLayout != null)
                _triangleLayout.AnimationStateChanged -= SetSlotsInputLocked;
        }

        public void Configure(ItemCategory inventoryCategory, string emptyLabel)
        {
            _inventoryCategory = inventoryCategory;
            _emptyLabel = emptyLabel;
            ResolveReferences();
            ConfigureInventory();
        }

        public void OnEnter()
        {
            EnsureInitialized();

            if (!HasSlots)
                return;

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

        private void ResolveReferences()
        {
            _detailPanel ??= GetComponentInChildren<ItemDetailPanel>(true);
            _inventory ??= GetComponentInChildren<Inventory>(true);

            if (_triangleLayout == null && _equipmentSlotsContainer != null)
                _triangleLayout = _equipmentSlotsContainer.GetComponent<TriangleLayout>();
        }

        private void ConfigureInventory()
        {
            _inventory?.SetSelectFirstSlotOnRefresh(false);
            _inventory?.SetFixedCategory(_inventoryCategory);
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
    }
}
