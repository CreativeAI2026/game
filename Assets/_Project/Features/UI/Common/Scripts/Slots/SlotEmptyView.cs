using UnityEngine;

namespace CreativeAI.UI
{
    public class SlotEmptyView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _emptyObject;

        private bool _hasWarnedMissingEmptyObject;

        public RectTransform EmptyRect =>
            _emptyObject != null ? _emptyObject.transform as RectTransform : null;

        public void SetEmpty(bool empty)
        {
            if (!ResolveEmptyObject())
                return;

            _emptyObject.SetActive(empty);
        }

        private bool ResolveEmptyObject()
        {
            if (_emptyObject != null)
                return true;

            if (!_hasWarnedMissingEmptyObject)
            {
                _hasWarnedMissingEmptyObject = true;
                Debug.LogWarning(
                    $"{nameof(SlotEmptyView)} '{name}' にEmpty表示Objectがないため、Empty表示切替をスキップします。Prefab上で設定してください。",
                    this
                );
            }

            return false;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_emptyObject != null)
                return;

            var emptyTransform =
                transform.Find("VisualRoot/EmptyText") ?? transform.Find("EmptyText");
            _emptyObject = emptyTransform != null ? emptyTransform.gameObject : null;
        }
#endif
    }
}
