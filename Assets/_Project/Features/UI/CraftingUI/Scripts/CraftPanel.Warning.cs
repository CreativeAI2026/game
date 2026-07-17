using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private const float WarningShakeDistance = 12f;
        private const float WarningShakeDuration = 0.6f;
        private const float WarningShakeFrequency = 5f;
        private Vector3 _warningTextBaseScale;
        private bool _hasWarningTextBaseScale;

        public void ShowMissingMaterialsWarning()
        {
            ShowWarning(_missingMaterialsMessage);
        }

        public void ShowEquippedMaterialWarning()
        {
            ShowWarning(_equippedMaterialMessage);
        }

        public void ShowCategoryMismatchWarning()
        {
            ShowWarning(_categoryMismatchMessage);
        }

        public void HideWarning()
        {
            if (_warningText != null)
                CaptureWarningBaseScale(_warningText.rectTransform);

            StopWarningAnimation();

            if (_warningText == null)
                return;

            ResetWarningTransform();

            if (_warningCanvasGroup != null)
                _warningCanvasGroup.alpha = 0f;

            _warningText.gameObject.SetActive(false);
        }

        private void ShowWarning(string message)
        {
            if (!ResolveWarningReferences())
                return;

            RectTransform warningTextRect = _warningText.rectTransform;
            CaptureWarningBaseScale(warningTextRect);
            StopWarningAnimation();

            _warningTextBasePosition = warningTextRect.anchoredPosition;
            _hasWarningTextBasePosition = true;

            _warningText.text = message;
            _warningText.gameObject.SetActive(true);

            ResetWarningTransform();

            _warningCanvasGroup.alpha = 1f;

            _warningRoutine = StartCoroutine(PlayWarningAnimationRoutine());
        }

        private IEnumerator PlayWarningAnimationRoutine()
        {
            float elapsed = 0f;
            while (elapsed < WarningShakeDuration)
            {
                float progress = Mathf.Clamp01(elapsed / WarningShakeDuration);
                float damping = 1f - progress;
                float offset =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * WarningShakeFrequency)
                    * WarningShakeDistance
                    * damping;
                SetWarningOffset(offset);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ResetWarningTransform();
            yield return new WaitForSecondsRealtime(WarningFadeDelay);

            elapsed = 0f;
            while (elapsed < WarningFadeDuration)
            {
                float progress = Mathf.Clamp01(elapsed / WarningFadeDuration);
                _warningCanvasGroup.alpha = 1f - progress;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _warningCanvasGroup.alpha = 0f;
            ResetWarningTransform();
            _warningText.gameObject.SetActive(false);
            _warningRoutine = null;
        }

        private void StopWarningAnimation()
        {
            if (_warningRoutine == null)
                return;

            StopCoroutine(_warningRoutine);
            _warningRoutine = null;
            ResetWarningTransform();
        }

        private void SetWarningOffset(float offsetX)
        {
            RectTransform warningTextRect =
                _warningText != null ? _warningText.rectTransform : null;
            if (warningTextRect == null)
                return;

            Vector2 basePosition = _hasWarningTextBasePosition
                ? _warningTextBasePosition
                : warningTextRect.anchoredPosition;
            warningTextRect.anchoredPosition = new Vector2(
                basePosition.x + offsetX,
                basePosition.y
            );
        }

        private void ResetWarningTransform()
        {
            SetWarningOffset(0f);
            RectTransform warningTextRect =
                _warningText != null ? _warningText.rectTransform : null;
            if (warningTextRect != null)
                warningTextRect.localScale = _hasWarningTextBaseScale
                    ? _warningTextBaseScale
                    : Vector3.one;
        }

        private void CaptureWarningBaseScale(RectTransform warningTextRect)
        {
            if (_hasWarningTextBaseScale || warningTextRect == null)
                return;

            _warningTextBaseScale = warningTextRect.localScale;
            _hasWarningTextBaseScale = true;
        }
    }
}
