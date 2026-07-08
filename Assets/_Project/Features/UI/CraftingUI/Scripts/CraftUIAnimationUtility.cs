using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftUIAnimationUtility
    {
        private const float PopDuration = 0.18f;
        private const float FadeDuration = 0.16f;
        private const float RowSlideDistance = 18f;
        private static readonly Dictionary<TMP_Text, Color> TextBaseColors = new();

        public static void PlayPopIn(GameObject target, float delay = 0f)
        {
            if (target == null)
                return;

            var rect = target.transform as RectTransform;
            if (rect == null)
                return;

            rect.DOKill();
            rect.localScale = Vector3.one * 0.82f;
            rect.DOScale(Vector3.one, PopDuration)
                .SetEase(Ease.OutBack)
                .SetDelay(delay)
                .SetUpdate(true);
        }

        public static void PlayResultIn(GameObject resultPanel)
        {
            if (resultPanel == null)
                return;

            var rect = resultPanel.transform as RectTransform;
            var canvasGroup = resultPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = resultPanel.AddComponent<CanvasGroup>();

            rect?.DOKill();
            canvasGroup.DOKill();

            if (rect != null)
                rect.localScale = Vector3.one * 0.9f;
            canvasGroup.alpha = 0f;

            resultPanel.SetActive(true);

            if (rect != null)
                rect.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
            canvasGroup.DOFade(1f, FadeDuration).SetUpdate(true);
        }

        public static void PlayRowIn(GameObject rowObject, int index)
        {
            if (rowObject == null)
                return;

            var rect = rowObject.transform as RectTransform;
            var canvasGroup = rowObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = rowObject.AddComponent<CanvasGroup>();

            if (rect == null)
                return;

            float delay = index * 0.05f;
            Vector2 basePosition = rect.anchoredPosition;
            rect.DOKill();
            canvasGroup.DOKill();
            rect.anchoredPosition = basePosition + Vector2.right * RowSlideDistance;
            canvasGroup.alpha = 0f;

            rect.DOAnchorPos(basePosition, 0.18f)
                .SetEase(Ease.OutQuad)
                .SetDelay(delay)
                .SetUpdate(true);
            canvasGroup.DOFade(1f, FadeDuration).SetDelay(delay).SetUpdate(true);
        }

        public static void PlayBump(RectTransform target)
        {
            if (target == null)
                return;

            target.DOKill();
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * 0.12f, 0.18f, 1, 0.4f).SetUpdate(true);
        }

        public static void PlayTextLimitWarning(TMP_Text text)
        {
            if (text == null)
                return;

            if (!TextBaseColors.TryGetValue(text, out Color baseColor))
            {
                baseColor = text.color;
                TextBaseColors[text] = baseColor;
            }

            text.DOKill();
            text.color = new Color(1f, 0.22f, 0.18f, baseColor.a);
            DOTween
                .To(() => text.color, value => text.color = value, baseColor, 0.22f)
                .SetTarget(text)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
            PlayBump(text.rectTransform);
        }
    }
}
