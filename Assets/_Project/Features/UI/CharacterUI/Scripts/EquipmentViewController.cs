using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public class EquipmentViewController : MonoBehaviour
    {
        [Header("Equipment Slots")]
        [SerializeField]
        private Transform _equipmentSlotsContainer;

        [Header("Detail")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Inventory")]
        [SerializeField]
        private Inventory _inventory;

        private static readonly Color SlotFrameSelected = new Color(1f, 0.78f, 0.15f, 0.9f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.15f);

        private List<EquipmentSlot> _slots;
        private int _currentSlotIndex;
        private ItemStack _selectedInventoryStack;
        private bool _resetInventoryTabOnNextEnter;

        private void Awake()
        {
            _detailPanel ??= GetComponentInChildren<ItemDetailPanel>(true);
            _inventory ??= GetComponentInChildren<Inventory>(true);

            // 装備画面のインベントリは、タブ変更だけでは選択や詳細を変えない。
            _inventory?.SetSelectFirstSlotOnRefresh(false);
        }

        private void Start()
        {
            InitializeSlots();
            EquipInitialTestItems();
            BindSlotButtons();
            BindInventoryEvents();

            SelectEquipmentSlot(0);
            _equipmentSlotsContainer.GetComponent<TriangleLayout>()?.RefreshLayout();
        }

        private void OnDestroy()
        {
            if (_slots != null)
            {
                foreach (var slot in _slots)
                {
                    if (slot == null)
                        continue;

                    slot.DoubleClicked -= OnEquipmentSlotDoubleClicked;
                    if (slot.Button != null)
                        slot.Button.onClick.RemoveAllListeners();
                }
            }

            if (_inventory != null)
            {
                _inventory.OnSlotClicked -= OnInventorySlotSelected;
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            }
        }

        public void OnEnter()
        {
            if (_slots == null || _slots.Count == 0)
                return;

            if (_resetInventoryTabOnNextEnter)
            {
                _resetInventoryTabOnNextEnter = false;
                _inventory?.ResetToFirstTab();
            }

            SelectEquipmentSlot(0);
            _selectedInventoryStack = null;
            _detailPanel?.Show(_slots[_currentSlotIndex].Item);
        }

        public void OnExit()
        {
            if (_slots == null || _slots.Count == 0)
                return;

            _detailPanel?.Clear();
            _selectedInventoryStack = null;
            _inventory?.ClearSelection();
        }

        public void ResetInventoryTab()
        {
            _resetInventoryTabOnNextEnter = true;
        }

        public void ResetViewState()
        {
            _resetInventoryTabOnNextEnter = false;
            _selectedInventoryStack = null;
            _inventory?.ResetViewState();

            if (_slots == null || _slots.Count == 0)
            {
                _detailPanel?.Clear();
                return;
            }

            SelectEquipmentSlot(0);
            _equipmentSlotsContainer?.GetComponent<TriangleLayout>()?.RotateSlotToTop(0);
            RefreshDetailFromSelectedEquipmentSlot();
        }

        private void InitializeSlots()
        {
            _slots = new List<EquipmentSlot>();
            if (_equipmentSlotsContainer == null)
                return;

            for (int i = 0; i < _equipmentSlotsContainer.childCount; i++)
            {
                var slotObject = _equipmentSlotsContainer.GetChild(i).gameObject;
                var slot = slotObject.GetComponent<EquipmentSlot>();
                if (slot == null)
                    slot = slotObject.AddComponent<EquipmentSlot>();

                slot.Init();
                slot.Clear();
                slot.DoubleClicked += OnEquipmentSlotDoubleClicked;
                _slots.Add(slot);
            }
        }

        private void EquipInitialTestItems()
        {
            var equipableItems = InventoryManager
                .Instance?.GetAllItems()
                .Where(stack =>
                    stack.Data.category == ItemCategory.Equipment
                    || stack.Data.category == ItemCategory.Food
                )
                .ToList();

            if (equipableItems == null || equipableItems.Count == 0)
                return;

            foreach (var stack in equipableItems)
                InventoryManager.Instance?.SetEquipped(stack, false);

            int equipCount = Mathf.Min(2, _slots.Count, equipableItems.Count);
            for (int i = 0; i < equipCount; i++)
            {
                _slots[i].Item = equipableItems[i].Data;
                InventoryManager.Instance?.SetEquipped(equipableItems[i], true);
                _slots[i].UpdateCount();
            }
        }

        private void BindSlotButtons()
        {
            if (_slots == null)
                return;

            for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
            {
                var button = _slots[slotIndex].GetComponent<Button>();
                if (button == null)
                    continue;

                int captured = slotIndex;
                button.onClick.AddListener(() => SelectEquipmentSlot(captured));
            }
        }

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotClicked += OnInventorySlotSelected;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        private void SelectEquipmentSlot(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Count)
                return;

            int previousSlotIndex = _currentSlotIndex;
            ItemData previousItem =
                previousSlotIndex >= 0 && previousSlotIndex < _slots.Count
                    ? _slots[previousSlotIndex].Item
                    : null;

            _currentSlotIndex = index;

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetFrameColor(i == index ? SlotFrameSelected : SlotFrameNormal);
                _slots[i].SetSelected(i == index);
            }

            _selectedInventoryStack = null;
            var selectedItem = _slots[index].Item;
            var selectedStack = InventoryManager
                .Instance?.GetAllItems()
                .Find(stack => stack.Data == selectedItem);

            _inventory?.SelectItem(selectedStack);

            bool changedBetweenEmptySlots =
                previousSlotIndex != index && previousItem == null && selectedItem == null;
            _detailPanel?.Show(selectedItem, "（未装備）", changedBetweenEmptySlots);
        }

        private void OnInventorySlotSelected(ItemStack stack)
        {
            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack?.Data);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (stack?.Data == null || _slots == null)
                return;

            int equippedSlotIndex = _slots.FindIndex(slot => slot.Item == stack.Data);
            if (stack.IsEquipped || equippedSlotIndex >= 0)
            {
                if (equippedSlotIndex >= 0)
                {
                    SelectEquipmentSlot(equippedSlotIndex);
                    _equipmentSlotsContainer
                        .GetComponent<TriangleLayout>()
                        ?.RotateSlotToTop(equippedSlotIndex);
                }

                _selectedInventoryStack = stack;
                UnequipCurrentSlot();
                return;
            }

            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack.Data);
            EquipSelectedItem();
        }

        private void OnEquipmentSlotDoubleClicked(EquipmentSlot slot)
        {
            if (_slots == null)
                return;

            int slotIndex = _slots.IndexOf(slot);
            if (slotIndex < 0)
                return;

            SelectEquipmentSlot(slotIndex);
            _selectedInventoryStack = null;
            UnequipCurrentSlot();
        }

        private void EquipSelectedItem()
        {
            if (_selectedInventoryStack == null)
                return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i == _currentSlotIndex)
                    continue;
                if (_slots[i].Item == _selectedInventoryStack.Data)
                    return;
            }

            var previousItem = _slots[_currentSlotIndex].Item;
            var previousStack = InventoryManager
                .Instance?.GetAllItems()
                .Find(stack => stack.Data == previousItem);
            InventoryManager.Instance?.SetEquipped(previousStack, false);
            _inventory?.UpdateItemEquippedState(previousStack, false, false);

            _slots[_currentSlotIndex].EquipAnimated(_selectedInventoryStack.Data);
            InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, true);

            _detailPanel?.Show(_selectedInventoryStack.Data);
            _inventory?.UpdateItemEquippedState(_selectedInventoryStack, true, true);
            SelectNextEmptyEquipmentSlot();
            RefreshDetailFromSelectedEquipmentSlot();
        }

        private void SelectNextEmptyEquipmentSlot()
        {
            if (_slots == null || _slots.Count <= 1)
                return;

            int equippedSlotIndex = _currentSlotIndex;
            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (equippedSlotIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Item != null)
                    continue;

                SelectEquipmentSlot(slotIndex);
                _equipmentSlotsContainer.GetComponent<TriangleLayout>()?.RotateSlotToTop(slotIndex);
                return;
            }
        }

        private void UnequipCurrentSlot()
        {
            if (_selectedInventoryStack != null && _selectedInventoryStack.IsEquipped)
            {
                InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, false);

                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Item != _selectedInventoryStack.Data)
                        continue;

                    _slots[i].ClearAnimated();
                    break;
                }

                _inventory?.UpdateItemEquippedState(_selectedInventoryStack, false, true);
                RefreshDetailFromSelectedEquipmentSlot();
                return;
            }

            var currentItem = _slots[_currentSlotIndex].Item;
            if (currentItem == null)
                return;

            var stack = InventoryManager
                .Instance?.GetAllItems()
                .Find(itemStack => itemStack.Data == currentItem);
            InventoryManager.Instance?.SetEquipped(stack, false);

            _slots[_currentSlotIndex].ClearAnimated();

            _inventory?.UpdateItemEquippedState(stack, false, false);
            RefreshDetailFromSelectedEquipmentSlot();
        }

        private void RefreshDetailFromSelectedEquipmentSlot()
        {
            if (
                _detailPanel == null
                || _slots == null
                || _currentSlotIndex < 0
                || _currentSlotIndex >= _slots.Count
            )
                return;

            _detailPanel.Show(_slots[_currentSlotIndex].Item, "（未装備）");
        }
    }
}
