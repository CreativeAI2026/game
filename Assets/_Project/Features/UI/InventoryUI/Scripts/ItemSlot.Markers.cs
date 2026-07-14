namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot
    {
        public void SetEquipped(bool isEquipped)
        {
            ResolveViewReferences();
            _markerView?.SetEquipped(isEquipped);
        }

        public void SetCraftAssigned(bool isAssigned)
        {
            ResolveViewReferences();
            _markerView?.SetCraftAssigned(isAssigned);
        }
    }
}
