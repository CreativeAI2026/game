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
        private Graphic[] _contentGraphics;
        private Color[] _contentBaseColors;
        private bool _idleDimmed;
        private bool _colorsCached;
        private bool _available = true;

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
                if (_idleDimmed && !_hovered)
                    target = Dim(target, 0.64f);
                if (!_available)
                    target = Dim(_normalColor, 0.46f);
                _background.color = Color.Lerp(
                    _background.color,
                    target,
                    1f - Mathf.Exp(-20f * Time.unscaledDeltaTime)
                );
            }
            UpdateContentColors();
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

        public void SetIdleDimmed(bool dimmed) => _idleDimmed = dimmed;

        public void SetAvailable(bool available)
        {
            _available = available;
            if (!available)
            {
                _hovered = false;
                _pressed = false;
            }
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
            if (_colorsCached)
                return;
            var graphics = GetComponentsInChildren<Graphic>(true);
            var content = new System.Collections.Generic.List<Graphic>();
            foreach (var graphic in graphics)
                if (graphic != null && graphic != _background && graphic != _activeAccent)
                    content.Add(graphic);
            _contentGraphics = content.ToArray();
            _contentBaseColors = new Color[_contentGraphics.Length];
            for (int i = 0; i < _contentGraphics.Length; i++)
                _contentBaseColors[i] = _contentGraphics[i].color;
            _colorsCached = true;
        }

        private void UpdateContentColors()
        {
            if (_contentGraphics == null || _contentBaseColors == null)
                return;
            float speed = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < _contentGraphics.Length; i++)
            {
                if (_contentGraphics[i] == null)
                    continue;
                Color target =
                    !_available ? Dim(_contentBaseColors[i], 0.42f)
                    : _idleDimmed && !_hovered ? Dim(_contentBaseColors[i], 0.72f)
                    : _contentBaseColors[i];
                _contentGraphics[i].color = Color.Lerp(_contentGraphics[i].color, target, speed);
            }
        }

        private static Color Dim(Color color, float multiplier) =>
            new(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);

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
