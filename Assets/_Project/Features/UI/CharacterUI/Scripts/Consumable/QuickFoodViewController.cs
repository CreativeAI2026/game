using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// キャラクターUI「即時使用食材」タブの View。所持食材リストから最大3つを
    /// 即時使用スロット(QuickFood)にセット/解除する。実際の消費は常駐の即時食材使用UIが行い、
    /// ここは「どの食材をクイック使用するか」の選択状態(<see cref="InventoryManager.GetQuickFoodSlots"/>)を編集するだけ。
    ///
    /// 装備品タブ(<see cref="EquipmentViewController"/>)と違い、セットしても在庫は減らず補正も付かない。
    /// スロットは在庫内スタックへの参照で、在庫から消えると InventoryService 側が自動で空にし
    /// <see cref="InventoryManager.QuickFoodChanged"/> を発火する。
    /// </summary>
    public class QuickFoodViewController : MonoBehaviour, ICharacterTabView
    {
        [Header("Quick Food Slots Root (子に EquipmentSlot を最大3つ)")]
        [SerializeField]
        private Transform _slotsRoot;

        [Header("Detail Panel")]
        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Food Inventory (所持食材リスト)")]
        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private string _emptyLabel = "（未セット）";

        private const ItemCategory Category = ItemCategory.Food;

        private readonly List<EquipmentSlot> _slots = new();
        private int _selectedSlotIndex;
        private bool _initialized;
        private bool _subscribedInventoryChanged;
        private bool _subscribedQuickFoodChanged;
        private bool _warnedMissingInventory;

        private bool HasSlots => _slots.Count > 0;

        private void Awake()
        {
            BindInventoryItemsRequested();
            _inventory?.SetSelectFirstSlotOnRefresh(false);
        }

        private void Start() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (_initialized)
                return;
            if (_inventory == null)
            {
                WarnMissingInventoryOnce();
                return;
            }

            BindInventoryItemsRequested();
            InitializeSlots();
            BindInventoryEvents();
            BindInventoryChanged();
            BindQuickFoodChanged();

            RefreshSlotsFromData();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (_inventory == null)
                return;

            BindInventoryItemsRequested();
            if (_initialized)
            {
                BindInventoryChanged();
                BindQuickFoodChanged();
            }
        }

        private void OnDisable()
        {
            UnbindInventoryItemsRequested();
            UnbindInventoryChanged();
            UnbindQuickFoodChanged();
        }

        private void OnDestroy()
        {
            UnbindSlots();
            UnbindInventoryEvents();
            UnbindInventoryItemsRequested();
            UnbindInventoryChanged();
            UnbindQuickFoodChanged();
        }

        // ---- ICharacterTabView ----

        public void OnEnter()
        {
            EnsureInitialized();
            RefreshSlotsFromData();
            _inventory?.RequestItems(Category, InventoryView.ScrollRefreshMode.KeepPosition);
            SelectSlot(0);
        }

        public void OnExit()
        {
            _detailPanel?.Clear();
            _inventory?.ClearSelection();
        }

        public void ResetViewState()
        {
            EnsureInitialized();
            _inventory?.ResetViewState();
            RefreshSlotsFromData();
            SelectSlot(0);
        }

        // ---- Slots ----

        private void InitializeSlots()
        {
            UnbindSlots();
            _slots.Clear();
            if (_slotsRoot == null)
                return;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slot = _slotsRoot.GetChild(i).GetComponent<EquipmentSlot>();
                if (slot == null)
                    continue;

                slot.Init();
                slot.Clear();
                slot.Clicked += OnQuickSlotClicked;
                slot.DoubleClicked += OnQuickSlotDoubleClicked;
                _slots.Add(slot);
            }
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnQuickSlotClicked;
                slot.DoubleClicked -= OnQuickSlotDoubleClicked;
            }
        }

        /// <summary>QuickFood データ(最大3)を各スロットに反映。単一ソースはデータ側。</summary>
        private void RefreshSlotsFromData()
        {
            if (!HasSlots)
                return;

            var data = InventoryManager.Instance?.GetQuickFoodSlots();
            for (int i = 0; i < _slots.Count; i++)
            {
                var stack = data != null && i < data.Count ? data[i] : null;
                if (stack != null)
                    _slots[i].SetStack(stack);
                else
                    _slots[i].Clear();
            }
        }

        private void SelectSlot(int index)
        {
            if (!HasSlots)
                return;

            index = Mathf.Clamp(index, 0, _slots.Count - 1);
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetSelected(i == index);
            _selectedSlotIndex = index;

            _detailPanel?.Show(_slots[index].Item, _emptyLabel);
        }

        private int FirstEmptySlotIndex()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Stack == null)
                    return i;
            return -1;
        }

        private void OnQuickSlotClicked(EquipmentSlot slot) => SelectSlot(_slots.IndexOf(slot));

        private void OnQuickSlotDoubleClicked(EquipmentSlot slot)
        {
            int index = _slots.IndexOf(slot);
            if (index < 0)
                return;

            SelectSlot(index);
            // ダブルクリックでそのスロットを解除(在庫は減らない・参照を外すだけ)。
            InventoryManager.Instance?.ClearQuickFood(index);
        }

        // ---- Food inventory list ----

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;
            _inventory.OnSlotClicked -= OnFoodSlotClicked;
            _inventory.OnSlotDoubleClicked -= OnFoodSlotDoubleClicked;
            _inventory.OnSlotClicked += OnFoodSlotClicked;
            _inventory.OnSlotDoubleClicked += OnFoodSlotDoubleClicked;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;
            _inventory.OnSlotClicked -= OnFoodSlotClicked;
            _inventory.OnSlotDoubleClicked -= OnFoodSlotDoubleClicked;
        }

        private void BindInventoryItemsRequested()
        {
            if (_inventory == null)
                return;
            _inventory.DisplayRefreshRequested -= OnDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnItemsRequested;
            _inventory.DisplayRefreshRequested += OnDisplayRefreshRequested;
            _inventory.ItemsRequested += OnItemsRequested;
        }

        private void UnbindInventoryItemsRequested()
        {
            if (_inventory == null)
                return;
            _inventory.DisplayRefreshRequested -= OnDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnItemsRequested;
        }

        private void OnDisplayRefreshRequested(
            TabDefinition _definition,
            int _tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        ) => _inventory?.RequestItems(Category, scrollMode);

        private void OnItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null || category != Category)
                return;
            var items = InventoryManager.Instance?.GetItemsByCategory(Category);
            _inventory.SetItems(items, scrollMode);
        }

        private void OnFoodSlotClicked(ItemStack stack)
        {
            if (!IsFood(stack))
                return;
            _detailPanel?.Show(stack.Data);
        }

        private void OnFoodSlotDoubleClicked(ItemStack stack)
        {
            if (!IsFood(stack) || !HasSlots)
                return;

            // 空きスロット優先。無ければ選択中スロットに置き換え。
            int target = FirstEmptySlotIndex();
            if (target < 0)
                target = _selectedSlotIndex;

            // SetQuickFood は同一スタックが別スロットにあれば移動、食材/在庫でなければ false。
            if (InventoryManager.Instance?.SetQuickFood(target, stack) == true)
            {
                SelectSlot(target);
                _detailPanel?.Show(stack.Data);
            }
        }

        private static bool IsFood(ItemStack stack) =>
            stack?.Data != null && stack.Data.category == Category;

        // ---- Change subscriptions ----

        private void BindInventoryChanged()
        {
            if (_subscribedInventoryChanged || InventoryManager.Instance == null)
                return;
            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
            _subscribedInventoryChanged = true;
        }

        private void UnbindInventoryChanged()
        {
            if (!_subscribedInventoryChanged)
                return;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            _subscribedInventoryChanged = false;
        }

        private void OnInventoryChanged()
        {
            _inventory?.RefreshCurrentTab();
            // スロットが参照する在庫数の反映(消費で0になったスロットは QuickFoodChanged 側で空になる)。
            foreach (var slot in _slots)
                if (slot?.Stack != null)
                    slot.UpdateCount();
        }

        private void BindQuickFoodChanged()
        {
            if (_subscribedQuickFoodChanged || InventoryManager.Instance == null)
                return;
            InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;
            InventoryManager.Instance.QuickFoodChanged += OnQuickFoodChanged;
            _subscribedQuickFoodChanged = true;
        }

        private void UnbindQuickFoodChanged()
        {
            if (!_subscribedQuickFoodChanged)
                return;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;
            _subscribedQuickFoodChanged = false;
        }

        private void OnQuickFoodChanged() => RefreshSlotsFromData();

        private void WarnMissingInventoryOnce()
        {
            if (_warnedMissingInventory)
                return;
            _warnedMissingInventory = true;
            Debug.LogWarning(
                $"{nameof(QuickFoodViewController)} '{name}' requires Inspector reference '_inventory'. Initialization was stopped.",
                this
            );
        }
    }
}
