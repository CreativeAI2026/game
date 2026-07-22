using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    public class InventoryPanelController : UIPanelStub
    {
        [SerializeField]
        private Inventory _inventory;

        [SerializeField]
        private ItemUseDialogPanel _itemUseDialogPanel;

        private bool _hasWarnedMissingInventory;
        private bool _hasWarnedMissingItemUseDialogPanel;
        private bool _hasWarnedInvalidTabDefinition;

        protected override void Awake()
        {
            base.Awake();
            WarnMissingReferencesOnce();
            _itemUseDialogPanel?.Hide();
        }

        private void OnEnable()
        {
            WarnMissingInventoryOnce();
            BindInventoryEvents();
            SubscribeToInventoryChanges();
        }

        private void OnDisable()
        {
            UnsubscribeFromInventoryChanges();
            UnbindInventoryEvents();
            _itemUseDialogPanel?.Hide();
        }

        private void OnDestroy()
        {
            UnsubscribeFromInventoryChanges();
            UnbindInventoryEvents();
        }

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnInventoryItemsRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested -= OnInventoryItemsRequested;
        }

        private void SubscribeToInventoryChanges()
        {
            if (_inventory == null || InventoryManager.Instance == null)
                return;

            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
        }

        private void UnsubscribeFromInventoryChanges()
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            _inventory?.RefreshCurrentTab();
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition definition,
            int tabIndex,
            Inventory.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null)
                return;

            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                _inventory.RequestItems(inventoryDefinition.Category, scrollMode);
                return;
            }

            WarnInvalidTabDefinitionOnce(tabIndex);
            _inventory.SetItems(null, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            Inventory.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null)
                return;

            var items = InventoryManager.Instance?.GetItemsByCategory(category);
            _inventory.SetItems(items, scrollMode);
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (stack?.Data is not FoodData)
                return;

            if (_itemUseDialogPanel == null)
            {
                WarnMissingItemUseDialogPanelOnce();
                return;
            }

            _itemUseDialogPanel.Show(stack);
        }

        private void WarnMissingReferencesOnce()
        {
            if (_inventory == null)
                WarnMissingInventoryOnce();
            if (_itemUseDialogPanel == null)
                WarnMissingItemUseDialogPanelOnce();
        }

        private void WarnMissingInventoryOnce()
        {
            if (_inventory != null || _hasWarnedMissingInventory)
                return;

            _hasWarnedMissingInventory = true;
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)} '{name}' の必須参照 '{nameof(_inventory)}' が未設定です。Inventoryイベントの購読をスキップします。Inspectorで設定してください。",
                this
            );
        }

        private void WarnMissingItemUseDialogPanelOnce()
        {
            if (_itemUseDialogPanel != null || _hasWarnedMissingItemUseDialogPanel)
                return;

            _hasWarnedMissingItemUseDialogPanel = true;
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)} '{name}' の必須参照 '{nameof(_itemUseDialogPanel)}' が未設定です。Food使用ダイアログの表示をスキップします。Inspectorで設定してください。",
                this
            );
        }

        private void WarnInvalidTabDefinitionOnce(int tabIndex)
        {
            if (_hasWarnedInvalidTabDefinition)
                return;

            _hasWarnedInvalidTabDefinition = true;
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)} '{name}' cannot resolve Inventory tab index {tabIndex}. Assign an {nameof(InventoryTabDefinition)} to every Inventory TabEntry.",
                this
            );
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _inventory ??= GetComponentInChildren<Inventory>(true);
            _itemUseDialogPanel ??= GetComponentInChildren<ItemUseDialogPanel>(true);
        }
#endif
    }
}
