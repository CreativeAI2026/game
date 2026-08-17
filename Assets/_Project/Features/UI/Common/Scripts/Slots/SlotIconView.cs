using System;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class SlotIconView : MonoBehaviour
    {
        [SerializeField]
        private Image _image;

        [SerializeField]
        private RectTransform _fitRect;

        [SerializeField, Range(0.1f, 1f)]
        private float _fillRatio = 0.9f;

        [SerializeField, Range(0f, 1f)]
        private float _emptyAlpha;

        private bool _hasWarnedMissingImage;
        private Color _baseColor = Color.white;
        private bool _hasCachedBaseColor;
        private bool _layoutRefreshPending;

        public bool HasRequiredReferences => ResolveImage();
        public bool IsVisible => _image != null && _image.gameObject.activeSelf;
        public RectTransform FitRect => _fitRect;
        public RectTransform IconRect => _image != null ? _image.rectTransform : null;

        public void SetIcon(ItemData item) => SetIcon(item != null ? item.icon : null);

        public void SetIcon(Sprite sprite)
        {
            if (!ResolveImage())
                return;

            _image.sprite = sprite;
            RefreshLayout();
            ApplyVisibility(sprite != null);
        }

        public void Clear() => SetIcon((Sprite)null);

        public void SetEmptyAlpha(float alpha)
        {
            _emptyAlpha = Mathf.Clamp01(alpha);
            if (ResolveImage())
                ApplyVisibility(_image.sprite != null);
        }

        public void PlayAppear(float duration)
        {
            if (!ResolveImage() || _image.sprite == null)
                return;

            KillTween();
            _image.gameObject.SetActive(true);
            _image.rectTransform.localScale = Vector3.one * 0.55f;
            SetAlpha(0f);
            _image
                .rectTransform.DOScale(Vector3.one, duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            _image.DOFade(1f, duration * 0.65f).SetUpdate(true);
        }

        public bool PlayHide(float duration, Action onComplete)
        {
            if (!ResolveImage() || !_image.gameObject.activeSelf)
                return false;

            KillTween();
            DOTween
                .Sequence()
                .SetUpdate(true)
                .Join(
                    _image.rectTransform.DOScale(Vector3.one * 0.35f, duration).SetEase(Ease.InBack)
                )
                .Join(_image.DOFade(0f, duration))
                .OnComplete(() => onComplete?.Invoke());
            return true;
        }

        public void KillTween()
        {
            if (_image == null)
                return;

            _image.DOKill();
            _image.rectTransform.DOKill();
        }

        public void ResetVisual()
        {
            if (!ResolveImage())
                return;

            KillTween();
            _image.rectTransform.localScale = Vector3.one;
            ApplyVisibility(_image.sprite != null);
        }

        public void RefreshLayout()
        {
            if (!ResolveImage())
                return;

            if (_fitRect == null)
                return;

            float shortEdge = Mathf.Min(_fitRect.rect.width, _fitRect.rect.height);
            if (shortEdge <= 0f)
            {
                _layoutRefreshPending = true;
                return;
            }

            float iconSize = shortEdge * _fillRatio;
            var iconRect = _image.rectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            var iconParent = iconRect.parent as RectTransform;
            if (iconParent != null && _fitRect != iconParent)
            {
                Vector3 fitCenterWorld = _fitRect.TransformPoint(_fitRect.rect.center);
                Vector3 fitCenterLocal = iconParent.InverseTransformPoint(fitCenterWorld);
                iconRect.localPosition = new Vector3(fitCenterLocal.x, fitCenterLocal.y, 0f);
            }
            else
            {
                iconRect.anchoredPosition = Vector2.zero;
            }

            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, iconSize);
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, iconSize);
            _image.preserveAspect = true;
            _layoutRefreshPending = false;
        }

        private void OnEnable()
        {
            if (!ResolveImage())
                return;

            RefreshLayout();
            ApplyVisibility(_image.sprite != null);
        }

        private void LateUpdate()
        {
            if (_layoutRefreshPending)
                RefreshLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
                RefreshLayout();
        }

        private bool ResolveImage()
        {
            if (_image == null)
            {
                WarnMissingImageOnce();
                return false;
            }

            if (!_hasCachedBaseColor)
            {
                _baseColor = _image.color;
                _hasCachedBaseColor = true;
            }

            return true;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_image != null)
                return;

            var iconTransform = transform.Find("VisualRoot/Icon") ?? transform.Find("Icon");
            _image = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            _fitRect ??= _image != null ? _image.rectTransform.parent as RectTransform : null;
        }
#endif

        private void ApplyVisibility(bool hasIcon)
        {
            bool visible = hasIcon || _emptyAlpha > 0f;
            _image.gameObject.SetActive(visible);
            SetAlpha(hasIcon ? 1f : _emptyAlpha);
        }

        private void SetAlpha(float alpha)
        {
            var color = _baseColor;
            color.a = alpha;
            _image.color = color;
        }

        private void WarnMissingImageOnce()
        {
            if (_hasWarnedMissingImage)
                return;

            _hasWarnedMissingImage = true;
            Debug.LogWarning(
                $"{nameof(SlotIconView)} '{name}' にIcon Imageがないため、Icon表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
