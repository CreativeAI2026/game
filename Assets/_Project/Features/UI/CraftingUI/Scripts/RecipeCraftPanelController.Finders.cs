#if UNITY_EDITOR
using System.Linq;
#endif

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanelController
    {
#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Auto Assign Main References")]
        private void AutoAssignMainReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanelController>(true);
            _recipeListView ??= GetComponentInChildren<RecipeListView>(true);

            _categoryTabGroup ??= GetComponentInChildren<CreativeAI.UI.TabGroup>(true);
            _materialListView ??= GetComponentInChildren<RecipeMaterialListView>(true);
            _detailPanel ??= GetComponentsInChildren<CreativeAI.UI.ItemDetailPanel>(true)
                .FirstOrDefault(panel =>
                    panel.GetComponentInParent<CreativeAI.UI.InventoryUI.InventoryView>(true)
                    == null
                );
        }
#endif
    }
}
