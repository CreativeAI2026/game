using System;
using UnityEngine;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class RevolverTabItemView : MonoBehaviour
    {
        [SerializeField]
        private TabButton _tabButton;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Action<int> _clicked;

        public int DataIndex { get; private set; } = -1;
        public TabDefinition Definition { get; private set; }
        public RectTransform RectTransform => _rectTransform;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public TabButton TabButton => _tabButton;
        public UnityEngine.UI.Button Button => _tabButton != null ? _tabButton.Button : null;
        public bool IsConfigured => _tabButton != null && _tabButton.Button != null;

        private void Awake()
        {
            CacheComponents();
        }

        public void Bind(TabDefinition definition, int dataIndex, Action<int> clicked)
        {
            Unbind();
            CacheComponents();
            Definition = definition;
            DataIndex = dataIndex;
            _clicked = clicked;

            _tabButton?.Bind(definition);
            if (Button != null)
                Button.onClick.AddListener(NotifyClicked);
        }

        public void Unbind()
        {
            if (Button != null)
                Button.onClick.RemoveListener(NotifyClicked);
            _clicked = null;
            Definition = null;
            DataIndex = -1;
        }

        public void ApplyLayout(RevolverTabLayout layout, bool interactionEnabled)
        {
            CacheComponents();
            _rectTransform.anchoredPosition = layout.AnchoredPosition;
            _rectTransform.localScale = Vector3.one * layout.Scale;
            _canvasGroup.alpha = layout.Alpha;
            _canvasGroup.interactable = interactionEnabled && layout.IsInteractable;
            _canvasGroup.blocksRaycasts = interactionEnabled && layout.IsInteractable;
            if (Button != null)
                Button.interactable = interactionEnabled && layout.IsInteractable;
        }

        public void SetSelected(bool selected)
        {
            _tabButton?.SetActive(selected, 0f, true, false);
        }

        private void CacheComponents()
        {
            _rectTransform ??= (RectTransform)transform;
            _canvasGroup ??= GetComponent<CanvasGroup>();
        }

        private void NotifyClicked()
        {
            if (DataIndex >= 0)
                _clicked?.Invoke(DataIndex);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
