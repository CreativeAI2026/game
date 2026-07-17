#if UNITY_EDITOR
using System.Linq;
#endif

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
#if UNITY_EDITOR
        [UnityEngine.ContextMenu("Auto Assign Main References")]
        private void AutoAssignMainReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanel>(true);
            _recipeList ??= CreativeAI.UI.UIChildFinder.Find(transform, "RecipeList");
            if (_recipeContent == null && _recipeList != null)
            {
                var scrollRect = _recipeList.GetComponent<UnityEngine.UI.ScrollRect>();
                _recipeContent = scrollRect != null ? scrollRect.content : null;
            }

            _categoryTabGroup ??= GetComponentInChildren<CreativeAI.UI.TabGroup>(true);
            _materialList ??= CreativeAI.UI.UIChildFinder.Find(transform, "MaterialList");
            _detailPanel ??= GetComponentsInChildren<CreativeAI.UI.ItemDetailPanel>(true)
                .FirstOrDefault(panel =>
                    panel.GetComponentInParent<CreativeAI.UI.InventoryUI.Inventory>(true) == null
                );
        }
#endif
    }
}
