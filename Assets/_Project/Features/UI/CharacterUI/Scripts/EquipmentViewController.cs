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

        [Header("Buttons")]
        [SerializeField]
        private Transform _equipButtonsContainer;

        private Button _equipButton;
        private Text _equipButtonText;
        private Button _unequipButton;

        private static readonly Color SlotFrameSelected = new Color(0.95f, 0.8f, 0.4f, 0.6f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.15f);

        private List<EquipmentSlot> _slots;
        private int _currentSlotIndex = 0;
        private ItemStack _selectedInventoryStack;
        private bool _resetInventoryTabOnNextEnter;

        private void Awake()
        {
            // 装備画面のインベントリは、タブ変更だけでは選択や詳細を変えない。
            _inventory?.SetSelectFirstSlotOnRefresh(false);
        }

        private void Start()
        {
            if (_equipButtonsContainer != null)
            {
                _equipButton = _equipButtonsContainer.GetChild(0).GetComponent<Button>();
                _equipButtonText = _equipButtonsContainer
                    .GetChild(0)
                    .GetComponentInChildren<Text>();
                _unequipButton = _equipButtonsContainer.GetChild(1).GetComponent<Button>();
            }

            _slots = new();
            for (int i = 0; i < _equipmentSlotsContainer.childCount; i++)
            {
                var slot = _equipmentSlotsContainer.GetChild(i).GetComponent<EquipmentSlot>();
                slot.Init();
                _slots.Add(slot);
            }

            // EquipmentとFoodカテゴリからランダムで装備
            var equipableItems = InventoryManager
                .Instance?.GetAllItems()
                .Where(s =>
                    s.Data.category == ItemCategory.Equipment
                    || s.Data.category == ItemCategory.Food
                )
                .ToList();

            if (equipableItems != null && equipableItems.Count > 0)
            {
                var shuffled = new List<ItemStack>(equipableItems);
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }

                int equipCount = Mathf.Min(2, _slots.Count, shuffled.Count);
                for (int i = 0; i < equipCount; i++)
                {
                    _slots[i].Item = shuffled[i].Data;
                    InventoryManager.Instance?.SetEquipped(shuffled[i], true);
                    _slots[i].UpdateCount();
                }
            }

            for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
            {
                var btn = _slots[slotIndex].GetComponent<Button>();
                if (btn != null)
                {
                    int captured = slotIndex;
                    btn.onClick.AddListener(() => SelectEquipmentSlot(captured));
                }
            }

            if (_inventory != null)
                _inventory.OnSlotClicked += OnInventorySlotSelected;

            if (_equipButton != null)
                _equipButton.onClick.AddListener(EquipSelectedItem);

            if (_unequipButton != null)
                _unequipButton.onClick.AddListener(UnequipCurrentSlot);

            SelectEquipmentSlot(0);
        }

        private void OnDestroy()
        {
            if (_slots != null)
                foreach (var slot in _slots)
                    if (slot.Button != null)
                        slot.Button.onClick.RemoveAllListeners();

            if (_inventory != null)
                _inventory.OnSlotClicked -= OnInventorySlotSelected;

            if (_equipButton != null)
                _equipButton.onClick.RemoveAllListeners();

            if (_unequipButton != null)
                _unequipButton.onClick.RemoveAllListeners();
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
            UpdateButtons();
        }

        private System.Collections.IEnumerator RefreshInventoryNextFrame()
        {
            yield return null;
            _inventory?.RefreshCurrentTab();
            yield return null; // もう1フレーム待つ
            _selectedInventoryStack = null;
            _detailPanel?.Show(_slots[_currentSlotIndex].Item);
            UpdateButtons();
        }

        public void OnExit()
        {
            if (_slots == null || _slots.Count == 0)
                return;
            _detailPanel?.Clear();
            _selectedInventoryStack = null;
            _detailPanel?.Show(_slots[_currentSlotIndex].Item);
            _selectedInventoryStack = null;
            UpdateButtons();
        }

        private void SelectEquipmentSlot(int i)
        {
            if (_slots == null || i < 0 || i >= _slots.Count)
                return;

            _currentSlotIndex = i;

            for (int j = 0; j < _slots.Count; j++)
            {
                _slots[j].SetFrameColor(j == i ? SlotFrameSelected : SlotFrameNormal);
                _slots[j].SetSelected(j == i);
            }

            _selectedInventoryStack = null;
            var selectedItem = _slots[i].Item;
            var selectedStack = InventoryManager
                .Instance?.GetAllItems()
                .Find(stack => stack.Data == selectedItem);

            _inventory?.SelectItem(selectedStack);
            _detailPanel?.Show(selectedItem);
            UpdateButtons();
        }

        public void ResetInventoryTab()
        {
            _resetInventoryTabOnNextEnter = true;
        }

        private void OnInventorySlotSelected(ItemStack stack)
        {
            _selectedInventoryStack = stack;
            _detailPanel?.Show(stack?.Data);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var currentSlotItem = _slots[_currentSlotIndex].Item;
            bool slotHasItem = currentSlotItem != null;
            bool inventoryItemSelected = _selectedInventoryStack != null;
            bool selectedIsEquipped = _selectedInventoryStack?.IsEquipped ?? false;

            if (inventoryItemSelected && selectedIsEquipped)
            {
                // 装備済みアイテムを選択中 → 外すのみ
                _equipButton?.gameObject.SetActive(false);
                _unequipButton?.gameObject.SetActive(true);
            }
            else if (inventoryItemSelected)
            {
                // 未装備アイテムを選択中 → 装備/変更
                _equipButton?.gameObject.SetActive(true);
                if (_equipButtonText != null)
                    _equipButtonText.text = slotHasItem ? "変更" : "装備";
                _unequipButton?.gameObject.SetActive(false);
            }
            else if (slotHasItem)
            {
                // インベントリ未選択、スロットにアイテムあり → 外すのみ
                _equipButton?.gameObject.SetActive(false);
                _unequipButton?.gameObject.SetActive(true);
            }
            else
            {
                _equipButton?.gameObject.SetActive(false);
                _unequipButton?.gameObject.SetActive(false);
            }
        }

        private void EquipSelectedItem()
        {
            if (_selectedInventoryStack == null)
                return;

            // 他のスロットに同じアイテムがあれば弾く
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i == _currentSlotIndex)
                    continue;
                if (_slots[i].Item == _selectedInventoryStack.Data)
                    return;
            }

            // 前の装備を解除
            var prevItem = _slots[_currentSlotIndex].Item;
            var prevStack = InventoryManager.Instance?.GetAllItems().Find(s => s.Data == prevItem);
            InventoryManager.Instance?.SetEquipped(prevStack, false);
            _inventory?.UpdateItemEquippedState(prevStack, false, false);

            // 新しく装備
            _slots[_currentSlotIndex].Item = _selectedInventoryStack.Data;
            InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, true);
            _slots[_currentSlotIndex].UpdateCount();

            _detailPanel?.Show(_selectedInventoryStack.Data);
            _inventory?.UpdateItemEquippedState(_selectedInventoryStack, true, true);
            UpdateButtons();
        }

        private void UnequipCurrentSlot()
        {
            // インベントリから装備済みアイテムを選んで外す場合
            if (_selectedInventoryStack != null && _selectedInventoryStack.IsEquipped)
            {
                InventoryManager.Instance?.SetEquipped(_selectedInventoryStack, false);

                // 装備スロットからも削除
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Item == _selectedInventoryStack.Data)
                    {
                        _slots[i].Item = null;
                        _slots[i].UpdateCount();
                        break;
                    }
                }

                _inventory?.UpdateItemEquippedState(_selectedInventoryStack, false, true);
                UpdateButtons();
                return;
            }

            // 装備スロット選択中の外す
            var currentItem = _slots[_currentSlotIndex].Item;
            if (currentItem == null)
                return;

            var stack = InventoryManager.Instance?.GetAllItems().Find(s => s.Data == currentItem);
            InventoryManager.Instance?.SetEquipped(stack, false);

            _slots[_currentSlotIndex].Item = null;
            _slots[_currentSlotIndex].UpdateCount();

            _inventory?.UpdateItemEquippedState(stack, false, false);
            _detailPanel?.Show(null);
            UpdateButtons();
        }
    }
}
