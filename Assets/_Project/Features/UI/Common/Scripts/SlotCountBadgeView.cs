using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.UI
{
    [ExecuteAlways]
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

        [SerializeField]
        private RectTransform _frameRect;

        [Header("Responsive Layout")]
        [SerializeField]
        [Min(0f)]
        private float _badgeRatio = 0.16f;

        [SerializeField]
        [Min(0f)]
        private float _minBadgeHeight = 12f;

        [SerializeField]
        [Min(0f)]
        private float _maxBadgeHeight = 24f;

        [SerializeField, Range(0.1f, 0.5f)]
        private float _badgeWidthRatio = 0.2f;

        [SerializeField]
        [Min(0f)]
        private float _fontRatio = 0.95f;

        [SerializeField]
        [Min(0f)]
        private float _minFontSize = 12f;

        [SerializeField]
        [Min(0f)]
        private float _maxFontSize = 26f;

        [SerializeField]
        private Vector2 _normalizedPosition = new(0.78f, 0.15f);

        private bool _hasWarnedMissingReferences;
        private bool _layoutRefreshPending;

        public bool HasRequiredReferences => ResolveReferences();
        public bool IsVisible => _container != null && _container.gameObject.activeSelf;

        public static bool ShouldShowCount(ItemData item, int count) =>
            item != null && item.MaxStack > 1 && count > 1;

        public void SetCount(ItemData item, int count)
        {
            if (!ResolveReferences())
                return;

            bool visible = ShouldShowCount(item, count);
            if (!visible)
            {
                Hide();
                return;
            }

            _countText.text = count.ToString();
            _container.gameObject.SetActive(true);
            _container.SetAsLastSibling();
            _countText.gameObject.SetActive(true);
            _countText.enabled = true;
            _countText.color = Color.white;
            _countText.alignment = TextAlignmentOptions.Center;
            _countText.overflowMode = TextOverflowModes.Overflow;
            _containerCanvasGroup.alpha = 1f;
            _countTextCanvasGroup.alpha = 1f;
            RefreshLayout();
            _layoutRefreshPending = true;
            _countText.ForceMeshUpdate();
        }

        public void RefreshLayout()
        {
            if (!ResolveReferences())
                return;

            float slotShortEdge = Mathf.Min(_frameRect.rect.width, _frameRect.rect.height);
            if (slotShortEdge <= 0f)
            {
                _layoutRefreshPending = true;
                return;
            }

            float badgeHeight = Mathf.Clamp(
                slotShortEdge * _badgeRatio,
                _minBadgeHeight,
                _maxBadgeHeight
            );
            float fontSize = Mathf.Clamp(badgeHeight * _fontRatio, _minFontSize, _maxFontSize);

            _countText.enableAutoSizing = false;
            _countText.fontSize = fontSize;
            float badgeWidth = slotShortEdge * _badgeWidthRatio;

            if (_container.parent is not RectTransform)
                return;

            Vector2 center = new(0.5f, 0.5f);
            _container.anchorMin = center;
            _container.anchorMax = center;
            _container.pivot = new Vector2(0.5f, 0.5f);
            Vector2 frameLocalPosition = new(
                (_normalizedPosition.x - _frameRect.pivot.x) * _frameRect.rect.width,
                (_normalizedPosition.y - _frameRect.pivot.y) * _frameRect.rect.height
            );
            Vector3 worldPosition = _frameRect.TransformPoint(frameLocalPosition);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, badgeWidth);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, badgeHeight);
            _container.position = worldPosition;
            _backgroundImage.enabled = false;
            _layoutRefreshPending = false;
        }

        private void LateUpdate()
        {
            if (_layoutRefreshPending && IsVisible)
            {
                Canvas.ForceUpdateCanvases();
                RefreshLayout();
            }
        }

        private void OnEnable()
        {
            if (_container != null && _container.gameObject.activeSelf)
                RefreshLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!EditorApplication.isUpdating && !EditorApplication.isCompiling)
                RefreshLayout();
        }
#endif

        public void Hide()
        {
            if (!ResolveReferences())
                return;

            KillTween();
            _countText.text = string.Empty;
            _countText.gameObject.SetActive(false);
            _containerCanvasGroup.alpha = 0f;
            _container.gameObject.SetActive(false);
            _layoutRefreshPending = false;
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
                && _backgroundImage != null
                && ResolveFrameRect();
            if (!valid)
                WarnMissingReferencesOnce();

            return valid;
        }

        private bool ResolveFrameRect()
        {
            if (_frameRect != null)
                return true;

            _frameRect = GetComponentInChildren<SlotFrameView>(true)?.FrameRect;
            return _frameRect != null;
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

            _frameRect ??= GetComponentInChildren<SlotFrameView>(true)?.FrameRect;
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
