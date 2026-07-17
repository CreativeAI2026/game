using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class RecipeMaterialListView : MonoBehaviour
    {
        [SerializeField]
        private List<RecipeMaterialRow> _rows = new();

        public bool HasRequiredReferences => _rows.Count > 0 && _rows.All(row => row != null);

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_rows.Count != 0)
                return;

            _rows = GetComponentsInChildren<RecipeMaterialRow>(true)
                .OrderBy(row => row.transform.GetSiblingIndex())
                .ToList();
        }
#endif

        public void ShowMaterials(CraftRecipeData recipe, int quantity)
        {
            ClearRows();
            if (recipe == null)
                return;

            var materials = recipe.Materials.Where(material => material != null).ToList();
            if (materials.Count == 0)
                return;

            gameObject.SetActive(true);
            int requiredQuantity = Mathf.Max(1, quantity);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null)
                    continue;

                bool hasMaterial = i < materials.Count;
                row.gameObject.SetActive(hasMaterial);
                if (!hasMaterial)
                    continue;

                row.Show(materials[i], requiredQuantity);
                CraftUIAnimationUtility.PlayRowIn(row.gameObject, i);
            }

            RebuildLayout();
        }

        public void Clear()
        {
            ClearRows();
            gameObject.SetActive(false);
        }

        public void RebuildLayout()
        {
            if (transform is not RectTransform rectTransform)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private void ClearRows()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    row.gameObject.SetActive(false);
            }
        }
    }
}
