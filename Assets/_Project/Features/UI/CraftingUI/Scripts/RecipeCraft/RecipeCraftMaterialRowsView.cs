using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public readonly struct RecipeCraftMaterialRowData
    {
        public RecipeCraftMaterialRowData(ItemData item, int requiredCount, int availableCount)
        {
            Item = item;
            RequiredCount = Mathf.Max(1, requiredCount);
            AvailableCount = Mathf.Max(0, availableCount);
        }

        public ItemData Item { get; }
        public int RequiredCount { get; }
        public int AvailableCount { get; }
        public bool IsSufficient => AvailableCount >= RequiredCount;
    }

    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.CraftingUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "RecipeMaterialListView"
    )]
    public sealed class RecipeCraftMaterialRowsView : MonoBehaviour
    {
        [SerializeField]
        private List<RecipeMaterialRow> _rows = new();

        private bool _warnedInsufficientCapacity;

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

        public void ShowRows(IReadOnlyList<RecipeCraftMaterialRowData> rows, bool animate = true)
        {
            if (rows == null || rows.Count == 0)
            {
                ClearRows();
                return;
            }

            if (rows.Count > _rows.Count)
            {
                if (!_warnedInsufficientCapacity)
                {
                    Debug.LogError(
                        $"{nameof(RecipeCraftMaterialRowsView)} on {name} received {rows.Count} rows, but only {_rows.Count} fixed rows are configured. Configure enough unique rows in the Inspector.",
                        this
                    );
                    _warnedInsufficientCapacity = true;
                }
            }
            else
            {
                _warnedInsufficientCapacity = false;
            }

            gameObject.SetActive(true);
            if (!animate)
                return;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null)
                    continue;

                bool hasRowData = i < rows.Count && rows[i].Item != null;
                row.gameObject.SetActive(hasRowData);
                if (!hasRowData)
                    continue;

                row.Show(rows[i]);
            }

            RebuildLayout();

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row != null && row.gameObject.activeSelf)
                    CraftUIAnimationUtility.PlayRowIn(row.gameObject, i);
            }
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
