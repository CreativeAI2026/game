using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話操作ボタンのホバー、押下、トグル状態を共通表現する。</summary>
    public sealed class ConversationControlButton
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerDownHandler,
            IPointerUpHandler
    {
        [SerializeField]
        private Image _background;

        [SerializeField]
        private RectTransform _keycap;

        [SerializeField]
        private Image _activeAccent;

        private readonly Color _normalColor = new(0.035f, 0.045f, 0.07f, 0.86f);
        private readonly Color _hoverColor = new(0.075f, 0.13f, 0.2f, 0.94f);
        private readonly Color _activeColor = new(0.16f, 0.42f, 0.62f, 0.96f);
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _keycapBasePosition;
        private float _feedbackUntil;
        private bool _hovered;
        private bool _pressed;
        private bool _active;
        private string _description;
        private ConversationControlBar _controlBar;

        public void Configure(
            Image background,
            RectTransform keycap,
            Image activeAccent,
            string description
        )
        {
            _background = background;
            _keycap = keycap;
            _activeAccent = activeAccent;
            _description = description;
            CacheBases();
            ApplyImmediate();
        }

        private void Awake()
        {
            _controlBar = GetComponentInParent<ConversationControlBar>();
            CacheBases();
            ApplyImmediate();
        }

        private void Update()
        {
            if (_hovered)
                ResolveControlBar()?.NotifyInteraction();
            bool feedback = Time.unscaledTime < _feedbackUntil;
            bool visuallyPressed = _pressed || feedback;
            float targetScale =
                visuallyPressed ? 0.985f
                : _hovered ? 1.035f
                : 1f;
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                _baseScale * targetScale,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime)
            );
            if (_background != null)
            {
                Color target =
                    _active ? _activeColor
                    : _hovered ? _hoverColor
                    : _normalColor;
                _background.color = Color.Lerp(
                    _background.color,
                    target,
                    1f - Mathf.Exp(-20f * Time.unscaledDeltaTime)
                );
            }
            if (_keycap != null)
            {
                Vector2 target =
                    _keycapBasePosition + (visuallyPressed ? Vector2.down * 2f : Vector2.zero);
                _keycap.anchoredPosition = Vector2.Lerp(
                    _keycap.anchoredPosition,
                    target,
                    1f - Mathf.Exp(-24f * Time.unscaledDeltaTime)
                );
            }
        }

        public void SetActiveState(bool active)
        {
            _active = active;
            if (_activeAccent != null)
                _activeAccent.gameObject.SetActive(active);
        }

        public void PlayShortcutFeedback()
        {
            _feedbackUntil = Time.unscaledTime + 0.12f;
            ResolveControlBar()?.NotifyInteraction();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ResolveControlBar()?.RequestTooltip(_description, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            ResolveControlBar()?.HideTooltip();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            ResolveControlBar()?.NotifyInteraction();
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        private void CacheBases()
        {
            _baseScale = transform.localScale;
            if (_keycap != null)
                _keycapBasePosition = _keycap.anchoredPosition;
        }

        private void ApplyImmediate()
        {
            if (_background != null)
                _background.color = _active ? _activeColor : _normalColor;
            if (_activeAccent != null)
                _activeAccent.gameObject.SetActive(_active);
        }

        private ConversationControlBar ResolveControlBar() =>
            _controlBar != null
                ? _controlBar
                : _controlBar = GetComponentInParent<ConversationControlBar>();
    }
}
