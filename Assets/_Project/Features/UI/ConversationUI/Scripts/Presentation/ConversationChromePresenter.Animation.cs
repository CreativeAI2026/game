using System.Collections;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    internal sealed partial class ConversationChromePresenter
    {
        public IEnumerator Show()
        {
            if (_root == null)
                yield break;
            _root.interactable = false;
            _root.blocksRaycasts = true;
            if (_root.alpha >= 0.999f)
            {
                _root.interactable = true;
                yield break;
            }
            CaptureWindowPosition();
            float duration = Mathf.Max(0.01f, _enterDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _root.alpha = t;
                if (_window != null && _hasWindowBasePosition)
                    _window.anchoredPosition =
                        _windowBasePosition + Vector2.up * Mathf.Lerp(_enterOffsetY, 0f, t);
                yield return null;
            }
            _root.alpha = 1f;
            _root.interactable = true;
            RestoreWindowPosition();
        }

        public IEnumerator Hide(float duration)
        {
            StopBounce();
            if (_root == null)
                yield break;
            float start = _root.alpha;
            duration = Mathf.Max(0.01f, duration);
            _root.interactable = false;
            _root.blocksRaycasts = false;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                _root.alpha = Mathf.Lerp(start, 0f, elapsed / duration);
                yield return null;
            }
            _root.alpha = 0f;
        }

        public IEnumerator HideLineText(float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float nameStart = _nameGroup != null ? _nameGroup.alpha : 0f;
            float bodyStart = _bodyGroup != null ? _bodyGroup.alpha : 0f;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_bodyGroup != null)
                    _bodyGroup.alpha = Mathf.Lerp(bodyStart, 0f, t);
                if (_nameGroup != null)
                    _nameGroup.alpha = Mathf.Lerp(nameStart, 0f, Mathf.Clamp01(t * 1.2f));
                yield return null;
            }
            if (_bodyGroup != null)
                _bodyGroup.alpha = 0f;
            if (_nameGroup != null)
                _nameGroup.alpha = 0f;
        }

        public void HideImmediate()
        {
            StopBounce();
            if (_root != null)
            {
                _root.alpha = 0f;
                _root.interactable = false;
                _root.blocksRaycasts = false;
            }
            RestoreWindowPosition();
        }

        private IEnumerator Bounce(RectTransform rect)
        {
            for (float elapsed = 0f; ; elapsed += Time.unscaledDeltaTime)
            {
                float phase = Mathf.Repeat(elapsed / Mathf.Max(0.01f, _bounceDuration), 1f);
                rect.anchoredPosition =
                    _indicatorBasePosition
                    + Vector2.up * CalculateIndicatorBounceOffset(phase, _bounceHeight);
                yield return null;
            }
        }

        private float CalculateIndicatorBounceOffset(float phase, float height) =>
            Mathf.Sin(Mathf.Clamp01(phase) * Mathf.PI) * height;

        private IEnumerator AnimateLineText(TMP_Text name, TMP_Text body, bool showName)
        {
            const float duration = 0.24f;
            const float bodyDelay = 0.06f;
            for (float elapsed = 0f; elapsed < duration + bodyDelay; elapsed += FrameDelta())
            {
                float nameT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float bodyT = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((elapsed - bodyDelay) / duration)
                );
                _nameGroup.alpha = showName ? nameT : 0f;
                _bodyGroup.alpha = bodyT;
                name.rectTransform.anchoredPosition = Vector2.Lerp(
                    _nameBasePosition + Vector2.down * 12f,
                    _nameBasePosition,
                    nameT
                );
                yield return null;
            }
            _nameGroup.alpha = showName ? 1f : 0f;
            _bodyGroup.alpha = 1f;
            name.rectTransform.anchoredPosition = _nameBasePosition;
            _lineTextAnimation = null;
        }
    }
}
