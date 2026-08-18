namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanelController
    {
        public void ShowMissingMaterialsWarning()
        {
            ShowWarning(CraftWarningKind.MissingMaterials);
        }

        public void ShowEquippedMaterialWarning()
        {
            ShowWarning(CraftWarningKind.EquippedMaterial);
        }

        public void ShowCategoryMismatchWarning()
        {
            ShowWarning(CraftWarningKind.CategoryMismatch);
        }

        public void ShowQuickFoodMaterialWarning()
        {
            ShowWarning(CraftWarningKind.QuickFoodMaterial);
        }

        public void HideWarning()
        {
            _warningToastView?.HideImmediate();
        }

        public void ShowWarning(CraftWarningKind kind)
        {
            if (!ValidateRequiredReference(_warningToastView, nameof(_warningToastView)))
                return;

            _warningToastView.Show(kind);
        }

        public void ShowWarning(string message)
        {
            if (!ValidateRequiredReference(_warningToastView, nameof(_warningToastView)))
                return;

            _warningToastView.Show(message);
        }
    }
}
