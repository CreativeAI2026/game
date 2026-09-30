using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class RevolverTabItemView : MonoBehaviour, IMoveHandler, ISubmitHandler
    {
        [SerializeField]
        private TabButton _tabButton;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Action<int> _clicked;
        private Action<AxisEventData> _moved;
        private Action<BaseEventData> _submitted;

        public int DataIndex { get; private set; } = -1;
        public TabDefinition Definition { get; private set; }
        public RectTransform RectTransform => _rectTransform;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public TabButton TabButton => _tabButton;
        public Button Button => _tabButton != null ? _tabButton.Button : null;
        public bool IsConfigured => _tabButton != null && _tabButton.Button != null;

        private void Awake()
        {
            CacheComponents();
        }

        public void Bind(TabDefinition definition, int dataIndex, Action<int> clicked)
        {
            Bind(definition, dataIndex, clicked, null, null);
        }

        public void Bind(
            TabDefinition definition,
            int dataIndex,
            Action<int> clicked,
            Action<AxisEventData> moved,
            Action<BaseEventData> submitted
        )
        {
            Unbind();
            CacheComponents();
            Definition = definition;
            DataIndex = dataIndex;
            _clicked = clicked;
            _moved = moved;
            _submitted = submitted;

            _tabButton?.Bind(definition);
            if (Button != null)
            {
                ConfigurePointerGraphics();
                var navigation = Button.navigation;
                navigation.mode = Navigation.Mode.None;
                Button.navigation = navigation;
                Button.onClick.AddListener(NotifyClicked);
            }
        }

        public void Unbind()
        {
            if (Button != null)
                Button.onClick.RemoveListener(NotifyClicked);
            _clicked = null;
            _moved = null;
            _submitted = null;
            Definition = null;
            DataIndex = -1;
        }

        public void ApplyLayout(RevolverTabLayout layout, bool interactionEnabled)
        {
            CacheComponents();
            _rectTransform.anchoredPosition = layout.AnchoredPosition;
            _rectTransform.localScale = Vector3.one * layout.Scale;
            _canvasGroup.alpha = layout.Alpha;
            bool canReceivePointer =
                interactionEnabled
                && layout.IsVisible
                && layout.IsInteractable
                && layout.Alpha > Mathf.Epsilon;
            _canvasGroup.interactable = canReceivePointer;
            _canvasGroup.blocksRaycasts = canReceivePointer;
            if (Button != null)
                Button.interactable = canReceivePointer;
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

        private void ConfigurePointerGraphics()
        {
            Graphic targetGraphic = Button.targetGraphic;
            if (targetGraphic == null || targetGraphic.color.a > Mathf.Epsilon)
                return;

            targetGraphic.raycastTarget = false;
            if (_tabButton.Icon != null && _tabButton.Icon.color.a > Mathf.Epsilon)
                _tabButton.Icon.raycastTarget = true;
        }

        private void NotifyClicked()
        {
            if (DataIndex >= 0)
                _clicked?.Invoke(DataIndex);
        }

        public void OnMove(AxisEventData eventData)
        {
            _moved?.Invoke(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            _submitted?.Invoke(eventData);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
