using CreativeAI.UI;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class CraftLoadingOverlayView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private RectTransform _gear;

        [SerializeField]
        private CanvasGroup _canvasGroup;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _root ??= gameObject;
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _gear ??= UIChildFinder.FindComponent<RectTransform>(transform, "LoadingGear");
        }
#endif

        public void Show()
        {
            if (_root == null)
                return;

            _root.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_gear != null)
            {
                _gear.localRotation = Quaternion.identity;
                _gear.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_gear != null)
            {
                _gear.localRotation = Quaternion.identity;
                _gear.gameObject.SetActive(false);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_root != null)
                _root.SetActive(false);
        }

        public void RotateGear(float speed)
        {
            if (_gear != null)
                _gear.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
        }
    }
}
