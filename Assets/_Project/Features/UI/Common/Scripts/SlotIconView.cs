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

        [SerializeField, Range(0f, 1f)]
        private float _emptyAlpha;

        private bool _hasWarnedMissingImage;
        private Color _baseColor = Color.white;
        private bool _hasCachedBaseColor;

        public bool HasRequiredReferences => ResolveImage();
        public bool IsVisible => _image != null && _image.gameObject.activeSelf;

        public void SetIcon(ItemData item) => SetIcon(item != null ? item.icon : null);

        public void SetIcon(Sprite sprite)
        {
            if (!ResolveImage())
                return;

            _image.sprite = sprite;
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
            DOTween.To(() => _image.color.a, SetAlpha, 1f, duration * 0.65f).SetUpdate(true);
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
                .Join(DOTween.To(() => _image.color.a, SetAlpha, 0f, duration))
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
