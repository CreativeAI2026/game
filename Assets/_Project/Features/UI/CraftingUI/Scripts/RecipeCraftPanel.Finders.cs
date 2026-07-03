using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private Transform Find(string objectName) => FindIn(transform, objectName);

        private Transform FindRecipeContent()
        {
            if (_recipeList == null)
                return null;

            var scrollRect = _recipeList.GetComponent<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
                return scrollRect.content;

            return FindIn(_recipeList, "Content") ?? _recipeList;
        }

        private ItemDetailPanel FindDetailPanel()
        {
            foreach (var panel in GetComponentsInChildren<ItemDetailPanel>(true))
            {
                if (panel.GetComponentInParent<Inventory>(true) == null)
                    return panel;
            }

            return GetComponentInChildren<ItemDetailPanel>(true);
        }

        private Transform GetCraftFlowRoot()
        {
            if (_craftPanel != null)
                return _craftPanel.transform;

            return transform.parent != null ? transform.parent : transform;
        }

        private static Transform FindIn(Transform root, string objectName)
        {
            return root == null ? null : UIChildFinder.Find(root, objectName);
        }

        private static Button FindButton(Transform root, string objectName)
        {
            return root == null ? null : UIChildFinder.FindButton(root, objectName);
        }

        private static T FindComponentIn<T>(Transform root, string objectName)
            where T : Component
        {
            return root == null ? null : UIChildFinder.FindComponent<T>(root, objectName);
        }

        private static GameObject FindGameObjectIn(Transform root, string objectName)
        {
            return root == null ? null : UIChildFinder.FindGameObject(root, objectName);
        }
    }
}
