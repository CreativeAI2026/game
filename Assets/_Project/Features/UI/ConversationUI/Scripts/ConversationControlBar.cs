using TMPro;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話操作バーの登場、無操作時の減光、ツールチップを管理する。</summary>
    public sealed class ConversationControlBar : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _group;

        [SerializeField]
        private TMP_Text _tooltip;

        [SerializeField]
        private CanvasGroup _tooltipGroup;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private float _enabledAt;
        private float _lastInteractionAt;
        private float _tooltipRequestedAt;
        private string _pendingTooltip;
        private RectTransform _tooltipSource;

        public void Configure(CanvasGroup group, TMP_Text tooltip, CanvasGroup tooltipGroup)
        {
            _group = group;
            _tooltip = tooltip;
            _tooltipGroup = tooltipGroup;
            CacheView();
        }

        private void Awake() => CacheView();

        private void OnEnable()
        {
            CacheView();
            _enabledAt = Time.unscaledTime;
            _lastInteractionAt = _enabledAt;
            if (_group != null)
                _group.alpha = 0f;
            if (_rect != null)
                _rect.anchoredPosition = _basePosition + Vector2.down * 10f;
            HideTooltip();
        }

        private void Update()
        {
            float intro = Mathf.Clamp01((Time.unscaledTime - _enabledAt) / 0.2f);
            float idle = Time.unscaledTime - _lastInteractionAt;
            float targetAlpha = idle >= 3.5f ? 0.58f : 1f;
            if (_group != null)
            {
                float introAlpha = Mathf.SmoothStep(0f, 1f, intro);
                _group.alpha = Mathf.MoveTowards(
                    _group.alpha,
                    targetAlpha * introAlpha,
                    Time.unscaledDeltaTime * 4.5f
                );
            }
            if (_rect != null)
                _rect.anchoredPosition = Vector2.Lerp(
                    _basePosition + Vector2.down * 10f,
                    _basePosition,
                    Mathf.SmoothStep(0f, 1f, intro)
                );

            bool shouldShowTooltip =
                !string.IsNullOrEmpty(_pendingTooltip)
                && Time.unscaledTime - _tooltipRequestedAt >= 0.45f;
            if (_tooltipGroup != null)
                _tooltipGroup.alpha = Mathf.MoveTowards(
                    _tooltipGroup.alpha,
                    shouldShowTooltip ? 1f : 0f,
                    Time.unscaledDeltaTime * 8f
                );
            if (shouldShowTooltip)
                PositionTooltip();
        }

        public void NotifyInteraction()
        {
            _lastInteractionAt = Time.unscaledTime;
        }

        public void RequestTooltip(string description, RectTransform source)
        {
            NotifyInteraction();
            _pendingTooltip = description;
            _tooltipSource = source;
            _tooltipRequestedAt = Time.unscaledTime;
            if (_tooltip != null)
                _tooltip.text = description;
        }

        public void HideTooltip()
        {
            _pendingTooltip = null;
            _tooltipSource = null;
        }

        private void CacheView()
        {
            _rect = transform as RectTransform;
            if (_rect != null)
                _basePosition = _rect.anchoredPosition;
        }

        private void PositionTooltip()
        {
            if (_tooltip == null || _tooltipSource == null)
                return;
            var tooltipRect = _tooltip.rectTransform.parent as RectTransform;
            if (tooltipRect == null)
                return;
            tooltipRect.anchoredPosition = new Vector2(
                _tooltipSource.anchoredPosition.x,
                _tooltipSource.anchoredPosition.y + 50f
            );
        }
    }
}
