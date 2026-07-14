namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot
    {
        protected override void RefreshSelectionVisuals()
        {
            ResolveViewReferences();
            _selectionView?.SetSelected(_isSlotSelected);
        }
    }
}
