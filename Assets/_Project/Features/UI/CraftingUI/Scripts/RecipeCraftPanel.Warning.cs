namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void PlayMissingMaterialsWarning()
        {
            GetCraftPanel()?.ShowMissingMaterialsWarning();
        }

        private void PlayEquippedMaterialWarning()
        {
            GetCraftPanel()?.ShowEquippedMaterialWarning();
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
