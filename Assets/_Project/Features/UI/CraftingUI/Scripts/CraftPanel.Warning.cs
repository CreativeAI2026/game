using DG.Tweening;
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

        public void ShowNotReadyWarning()
        {
            ShowWarning(_notReadyMessage);
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

            _warningText.DOKill();
            _warningTextRect?.DOKill();
            _warningTextCanvasGroup?.DOKill();
            ResetWarningTransform();

            if (_warningTextCanvasGroup != null)
                _warningTextCanvasGroup.alpha = 0f;

            _warningText.gameObject.SetActive(false);
            _activeWarningMessage = null;
        }

        private void ShowWarning(string message)
        {
            if (!ResolveWarningReferences())
                return;

            StopWarningAnimation();
            _warningText.DOKill();
            _warningTextRect?.DOKill();
            _warningTextCanvasGroup?.DOKill();

            _activeWarningMessage = message;
            _warningText.text = message;
            _warningText.gameObject.SetActive(true);

            ResetWarningTransform();

            if (_warningTextCanvasGroup != null)
                _warningTextCanvasGroup.alpha = 1f;

            PlayWarningAnimation();
        }

        private void PlayWarningAnimation()
        {
            if (_warningTextRect == null)
                return;

            Vector2 basePosition = GetWarningBasePosition();

            _warningTextRect.anchoredPosition = basePosition;

            _warningSequence = DOTween.Sequence().SetUpdate(true);

            _warningSequence.Append(
                _warningTextRect.DOPunchAnchorPos(
                    Vector2.right * WarningShakeDistance,
                    0.28f,
                    14,
                    0.7f
                )
            );

            _warningSequence.AppendInterval(WarningFadeDelay);

            if (_warningTextCanvasGroup != null)
                _warningSequence.Append(_warningTextCanvasGroup.DOFade(0f, WarningFadeDuration));

            _warningSequence.OnComplete(() =>
            {
                ResetWarningTransform();

                if (_warningText != null)
                    _warningText.gameObject.SetActive(false);

                _warningSequence = null;
                _activeWarningMessage = null;
            });
        }

        private void StopWarningAnimation()
        {
            if (_warningSequence == null)
                return;

            _warningSequence.Kill();
            _warningSequence = null;
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

        private Vector2 GetWarningBasePosition()
        {
            if (_warningTextRect == null)
                return Vector2.zero;

            return _hasWarningTextBasePosition
                ? _warningTextBasePosition
                : _warningTextRect.anchoredPosition;
        }

        private void ResetWarningTransform()
        {
            SetWarningOffset(0f);
            if (_warningTextRect != null)
                _warningTextRect.localScale = Vector3.one;
        }
    }
}
