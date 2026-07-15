using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class SlotCountBadgeView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _container;

        [SerializeField]
        private TMP_Text _countText;

        [SerializeField]
        private CanvasGroup _containerCanvasGroup;

        [SerializeField]
        private CanvasGroup _countTextCanvasGroup;

        [SerializeField]
        private Image _backgroundImage;

        [Header("Responsive Layout")]
        [SerializeField]
        [Min(0f)]
        private float _badgeRatio = 0.32f;

        [SerializeField]
        [Min(0f)]
        private float _minBadgeHeight = 20f;

        [SerializeField]
        [Min(0f)]
        private float _maxBadgeHeight = 34f;

        [SerializeField]
        [Min(0f)]
        private float _horizontalPadding = 10f;

        [SerializeField]
        [Min(0f)]
        private float _fontRatio = 0.72f;

        [SerializeField]
        [Min(0f)]
        private float _minFontSize = 12f;

        [SerializeField]
        [Min(0f)]
        private float _maxFontSize = 20f;

        [SerializeField]
        private Vector2 _anchoredPosition = new(-4f, 4f);

        private bool _hasWarnedMissingReferences;

        public bool HasRequiredReferences => ResolveReferences();
        public bool IsVisible => _container != null && _container.gameObject.activeSelf;

        public void SetCount(ItemData item, int count)
        {
            if (!ResolveReferences())
                return;

            bool visible = item != null && item.MaxStack > 1 && count > 1;
            if (!visible)
            {
                Hide();
                return;
            }

            _countText.text = count.ToString();
            _container.gameObject.SetActive(true);
            _countText.gameObject.SetActive(true);
            _containerCanvasGroup.alpha = 1f;
            _countTextCanvasGroup.alpha = 1f;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (!ResolveReferences() || transform is not RectTransform slotRect)
                return;

            float slotShortEdge = Mathf.Min(slotRect.rect.width, slotRect.rect.height);
            if (slotShortEdge <= 0f)
                return;

            float badgeHeight = Mathf.Clamp(
                slotShortEdge * _badgeRatio,
                _minBadgeHeight,
                _maxBadgeHeight
            );
            float fontSize = Mathf.Clamp(badgeHeight * _fontRatio, _minFontSize, _maxFontSize);

            _countText.enableAutoSizing = false;
            _countText.fontSize = fontSize;
            float preferredTextWidth = _countText.GetPreferredValues(_countText.text).x;
            float badgeWidth = Mathf.Max(badgeHeight, preferredTextWidth + _horizontalPadding);
            if (_countText.text.Length >= 2)
                badgeWidth = Mathf.Max(badgeWidth, badgeHeight + _horizontalPadding);

            Vector2 bottomRight = new(1f, 0f);
            _container.anchorMin = bottomRight;
            _container.anchorMax = bottomRight;
            _container.pivot = bottomRight;
            _container.anchoredPosition = _anchoredPosition;
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, badgeWidth);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, badgeHeight);
        }

        public void Hide()
        {
            if (!ResolveReferences())
                return;

            KillTween();
            _countText.text = string.Empty;
            _countText.gameObject.SetActive(false);
            _containerCanvasGroup.alpha = 0f;
            _container.gameObject.SetActive(false);
        }

        public void AnimateAppear(float duration)
        {
            if (!ResolveReferences() || !IsVisible)
                return;

            _countText.rectTransform.DOKill();
            _countTextCanvasGroup.DOKill();
            _countText.rectTransform.localScale = Vector3.one * 0.7f;
            _countTextCanvasGroup.alpha = 1f;
            _countText
                .rectTransform.DOScale(Vector3.one, duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void PlayHide(float duration)
        {
            if (!ResolveReferences() || !IsVisible)
                return;

            _countText.rectTransform.DOKill();
            _countTextCanvasGroup.DOKill();
            _countText.rectTransform.DOScale(Vector3.one * 0.35f, duration).SetUpdate(true);
            _countTextCanvasGroup.DOFade(0f, duration).SetUpdate(true);
        }

        public void KillTween()
        {
            if (_countText != null)
                _countText.rectTransform.DOKill();
            _countTextCanvasGroup?.DOKill();
            _containerCanvasGroup?.DOKill();
        }

        public void ResetVisual()
        {
            if (!ResolveReferences())
                return;

            KillTween();
            _countText.rectTransform.localScale = Vector3.one;
            _countTextCanvasGroup.alpha = 1f;
            _containerCanvasGroup.alpha = IsVisible ? 1f : 0f;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || !IsVisible)
                return;

            RefreshLayout();
        }

        private bool ResolveReferences()
        {
            bool valid =
                _container != null
                && _countText != null
                && _containerCanvasGroup != null
                && _countTextCanvasGroup != null
                && _backgroundImage != null;
            if (!valid)
                WarnMissingReferencesOnce();

            return valid;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _container ??= FindContainer();
            if (_container != null)
            {
                _countText ??= _container.GetComponentInChildren<TMP_Text>(true);
                _containerCanvasGroup ??= _container.GetComponent<CanvasGroup>();
                _backgroundImage ??= _container.GetComponent<Image>();
            }

            if (_countText != null)
                _countTextCanvasGroup ??= _countText.GetComponent<CanvasGroup>();
        }

        private RectTransform FindContainer()
        {
            if (
                transform is RectTransform selfRect
                && (name == "CountBadge" || name == "numberSlot")
            )
                return selfRect;

            return transform.Find("VisualRoot/CountBadge") as RectTransform
                ?? transform.Find("VisualRoot/numberSlot") as RectTransform
                ?? transform.Find("CountBadge") as RectTransform
                ?? transform.Find("numberSlot") as RectTransform;
        }
#endif

        private void WarnMissingReferencesOnce()
        {
            if (_hasWarnedMissingReferences)
                return;

            _hasWarnedMissingReferences = true;
            Debug.LogWarning(
                $"{nameof(SlotCountBadgeView)} '{name}' のCountBadge、CountText、Image、CanvasGroup参照が不足しているため、Count表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
