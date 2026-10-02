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

            _categoryTabGroup ??= GetComponentsInChildren<CreativeAI.UI.TabGroup>(true)
                .FirstOrDefault(group =>
                    group.EntryCount == 2
                    && group.GetView(0) == null
                    && group.GetView(1) == null
                    && group.GetDefinitionForEntry(0)
                        is CreativeAI.UI.InventoryUI.InventoryTabDefinition equipmentDefinition
                    && equipmentDefinition.Category == CreativeAI.Gameplay.ItemCategory.Equipment
                    && group.GetDefinitionForEntry(1)
                        is CreativeAI.UI.InventoryUI.InventoryTabDefinition foodDefinition
                    && foodDefinition.Category == CreativeAI.Gameplay.ItemCategory.Food
                );
            _materialRowsView ??= GetComponentInChildren<RecipeCraftMaterialRowsView>(true);
            _detailPanel ??= GetComponentsInChildren<CreativeAI.UI.ItemDetailPanel>(true)
                .FirstOrDefault(panel =>
                    panel.GetComponentInParent<CreativeAI.UI.InventoryUI.InventoryView>(true)
                    == null
                );
        }
#endif
    }
}
