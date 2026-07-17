namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanelController
    {
        private void PlayMissingMaterialsWarning()
        {
            GetCraftPanel()?.ShowWarning(CraftWarningKind.MissingMaterials);
        }

        private void PlayEquippedMaterialWarning()
        {
            GetCraftPanel()?.ShowWarning(CraftWarningKind.EquippedMaterial);
        }

        private void PlayQuickFoodMaterialWarning()
        {
            GetCraftPanel()?.ShowQuickFoodMaterialWarning();
        }

        private void HideWarningImmediately()
        {
            GetCraftPanel()?.HideWarning();
        }
    }
}
