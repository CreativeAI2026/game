using System;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftQuantityDialogAnimation
    {
        public static void PlayOpen(
            GameObject panel,
            GameObject dialog,
            RectTransform dialogRect,
            CanvasGroup canvasGroup,
            float startScale,
            float duration,
            Action onOpened = null
        )
        {
            if (dialog == null)
                return;

            Kill(dialogRect, canvasGroup);

            if (panel != null)
                panel.SetActive(true);
            dialog.SetActive(true);

            if (dialogRect != null)
                dialogRect.localScale = Vector3.one * startScale;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (dialogRect != null)
                sequence.Join(dialogRect.DOScale(Vector3.one, duration).SetEase(Ease.OutBack));
            if (canvasGroup != null)
                sequence.Join(canvasGroup.DOFade(1f, duration * 0.75f));

            sequence.OnComplete(() =>
            {
                if (canvasGroup != null)
                {
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }

                onOpened?.Invoke();
            });
        }

        public static void PlayClose(
            GameObject panel,
            GameObject dialog,
            RectTransform dialogRect,
            CanvasGroup canvasGroup,
            float endScale,
            float duration,
            Action onClosed = null
        )
        {
            if (dialog == null || !dialog.activeSelf)
                return;

            Kill(dialogRect, canvasGroup);
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);
            if (dialogRect != null)
            {
                sequence.Join(
                    dialogRect.DOScale(Vector3.one * endScale, duration).SetEase(Ease.InBack)
                );
            }
            if (canvasGroup != null)
                sequence.Join(canvasGroup.DOFade(0f, duration * 0.75f));

            sequence.OnComplete(() =>
            {
                dialog.SetActive(false);
                if (panel != null)
                    panel.SetActive(false);

                onClosed?.Invoke();
            });
        }

        public static void Kill(RectTransform dialogRect, CanvasGroup canvasGroup)
        {
            dialogRect?.DOKill();
            canvasGroup?.DOKill();
        }
    }
}
