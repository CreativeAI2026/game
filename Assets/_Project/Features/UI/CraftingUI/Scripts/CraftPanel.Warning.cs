using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
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

            StopWarningAnimation();

            _warningText.text = message;
            _warningText.gameObject.SetActive(true);

            ResetWarningTransform();

            _warningCanvasGroup.alpha = 1f;

            _warningRoutine = StartCoroutine(PlayWarningAnimationRoutine());
        }

        private IEnumerator PlayWarningAnimationRoutine()
        {
            const float shakeDuration = 0.28f;
            const float shakeFrequency = 14f;
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                float progress = Mathf.Clamp01(elapsed / shakeDuration);
                float damping = 1f - progress;
                float offset =
                    Mathf.Sin(progress * Mathf.PI * shakeFrequency)
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
            if (_warningTextRect == null)
                return;

            Vector2 basePosition = _hasWarningTextBasePosition
                ? _warningTextBasePosition
                : _warningTextRect.anchoredPosition;
            _warningTextRect.anchoredPosition = basePosition + Vector2.right * offsetX;
        }

        private void ResetWarningTransform()
        {
            SetWarningOffset(0f);
            if (_warningTextRect != null)
                _warningTextRect.localScale = Vector3.one;
        }
    }
}
