using UnityEngine;

namespace CreativeAI.UI
{
    public class SlotSelectionView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _selectedFrame;

        private bool _hasWarnedMissingSelectedFrame;

        public void SetSelected(bool selected)
        {
            if (!ResolveSelectedFrame())
                return;

            _selectedFrame.SetActive(selected);
        }

        private bool ResolveSelectedFrame()
        {
            if (_selectedFrame != null)
                return true;

            if (!_hasWarnedMissingSelectedFrame)
            {
                _hasWarnedMissingSelectedFrame = true;
                Debug.LogWarning(
                    $"{nameof(SlotSelectionView)} '{name}' にSelectedFrameがないため、選択表示をスキップします。Prefab上で設定してください。",
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
            if (_selectedFrame != null)
                return;

            var frameTransform =
                transform.Find("VisualRoot/SelectedFrame") ?? transform.Find("SelectedFrame");
            _selectedFrame = frameTransform != null ? frameTransform.gameObject : null;
        }
#endif
    }
}
