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

        private bool _hasWarnedMissingItemUseDialogPanel;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            _itemUseDialogPanel?.Hide();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindInventoryEvents();
        }

        private void OnDisable()
        {
            UnbindInventoryEvents();
            _itemUseDialogPanel?.Hide();
        }

        private void OnDestroy()
        {
            UnbindInventoryEvents();
        }

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
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

        private void ResolveReferences()
        {
            _inventory ??= GetComponentInChildren<Inventory>(true);
            _itemUseDialogPanel ??= GetComponentInChildren<ItemUseDialogPanel>(true);

            if (_itemUseDialogPanel == null)
                WarnMissingItemUseDialogPanelOnce();
        }

        private void WarnMissingItemUseDialogPanelOnce()
        {
            if (_hasWarnedMissingItemUseDialogPanel)
                return;

            _hasWarnedMissingItemUseDialogPanel = true;
            Debug.LogWarning(
                $"{nameof(InventoryPanelController)} '{name}' に {nameof(ItemUseDialogPanel)} が設定されておらず、既存の子Objectからも見つかりません。Inspectorで参照を設定してください。",
                this
            );
        }
    }
}
