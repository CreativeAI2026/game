using System;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    public abstract partial class BaseItemSlot
    {
        private const float ItemTransitionDuration = 0.2f;

        public void SetItemAnimated(ItemData item, int count = 1)
        {
            SetItem(item, count);
            if (_iconImage == null || item == null || item.icon == null)
                return;

            var iconRect = _iconImage.rectTransform;
            iconRect.localScale = Vector3.one * 0.55f;
            _iconImage.color = new Color(1f, 1f, 1f, 0f);

            iconRect
                .DOScale(Vector3.one, ItemTransitionDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            _iconImage.DOFade(1f, ItemTransitionDuration * 0.65f).SetUpdate(true);

            if (_countText != null && _countText.gameObject.activeSelf)
                AnimateCountAppear();
        }

        public void ClearAnimated(Action onComplete = null)
        {
            InitializeBase();
            KillItemTransition();

            _item = null;
            _count = 0;

            if (_iconImage == null || !_iconImage.gameObject.activeSelf)
            {
                Clear();
                onComplete?.Invoke();
                return;
            }

            var iconRect = _iconImage.rectTransform;
            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(
                iconRect.DOScale(Vector3.one * 0.35f, ItemTransitionDuration).SetEase(Ease.InBack)
            );
            sequence.Join(_iconImage.DOFade(0f, ItemTransitionDuration));

            if (_countText != null && _countText.gameObject.activeSelf)
            {
                sequence.Join(
                    _countText.rectTransform.DOScale(Vector3.one * 0.35f, ItemTransitionDuration)
                );
                if (_countCanvasGroup != null)
                    sequence.Join(_countCanvasGroup.DOFade(0f, ItemTransitionDuration));
            }

            sequence.OnComplete(() =>
            {
                Clear();
                onComplete?.Invoke();
            });
        }

        private void AnimateCountAppear()
        {
            _countCanvasGroup.alpha = 1f;
            _countText.rectTransform.localScale = Vector3.one * 0.7f;
            _countText
                .rectTransform.DOScale(Vector3.one, ItemTransitionDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        private void KillItemTransition()
        {
            if (_iconImage != null)
            {
                _iconImage.DOKill();
                _iconImage.rectTransform.DOKill();
            }

            if (_countText != null)
            {
                _countText.DOKill();
                _countText.rectTransform.DOKill();
                _countCanvasGroup?.DOKill();
            }
        }

        private void ResetItemVisuals()
        {
            if (_iconImage != null)
            {
                _iconImage.rectTransform.localScale = Vector3.one;
                var color = _iconImage.color;
                color.a = 1f;
                _iconImage.color = color;
            }

            if (_countText != null)
            {
                _countText.rectTransform.localScale = Vector3.one;
                var color = _countText.color;
                color.a = 1f;
                _countText.color = color;
                if (_countCanvasGroup != null)
                    _countCanvasGroup.alpha = 1f;
            }
        }
    }
}
