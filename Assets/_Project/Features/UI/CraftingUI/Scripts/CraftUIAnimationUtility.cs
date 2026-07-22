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
        private const float ResultAnimationDuration = 0.22f;
        private const float ResultHiddenScale = 0.9f;
        private static readonly Dictionary<TMP_Text, Color> TextBaseColors = new();
        private static readonly HashSet<GameObject> WarnedMissingCanvasGroups = new();

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
            {
                WarnMissingCanvasGroupOnce(resultPanel, "ResultPanel");
                return;
            }

            DOTween.Kill(resultPanel);
            rect?.DOKill();
            canvasGroup.DOKill();

            if (rect != null)
                rect.localScale = Vector3.one * ResultHiddenScale;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;

            resultPanel.SetActive(true);

            var sequence = DOTween.Sequence().SetTarget(resultPanel).SetUpdate(true);
            if (rect != null)
                sequence.Join(
                    rect.DOScale(Vector3.one, ResultAnimationDuration).SetEase(Ease.OutBack)
                );
            sequence.Join(
                DOTween.To(
                    () => canvasGroup.alpha,
                    value => canvasGroup.alpha = value,
                    1f,
                    FadeDuration
                )
            );
            sequence.OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });
        }

        public static void PlayResultOut(GameObject resultPanel, System.Action onComplete = null)
        {
            if (resultPanel == null)
                return;

            if (!resultPanel.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            var rect = resultPanel.transform as RectTransform;
            var canvasGroup = resultPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                WarnMissingCanvasGroupOnce(resultPanel, "ResultPanel");
                return;
            }

            DOTween.Kill(resultPanel);
            rect?.DOKill();
            canvasGroup.DOKill();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var sequence = DOTween.Sequence().SetTarget(resultPanel).SetUpdate(true);
            if (rect != null)
            {
                sequence.Join(
                    rect.DOScale(Vector3.one * ResultHiddenScale, ResultAnimationDuration)
                        .SetEase(Ease.InBack)
                );
            }
            sequence.Join(
                DOTween.To(
                    () => canvasGroup.alpha,
                    value => canvasGroup.alpha = value,
                    0f,
                    FadeDuration
                )
            );
            sequence.OnComplete(() =>
            {
                resultPanel.SetActive(false);
                if (rect != null)
                    rect.localScale = Vector3.one;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                onComplete?.Invoke();
            });
        }

        public static void HideResultImmediately(GameObject resultPanel)
        {
            if (resultPanel == null)
                return;

            DOTween.Kill(resultPanel);
            if (resultPanel.transform is RectTransform rect)
            {
                rect.DOKill();
                rect.localScale = Vector3.one;
            }

            var canvasGroup = resultPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            resultPanel.SetActive(false);
        }

        public static void PlayRowIn(GameObject rowObject, int index)
        {
            if (rowObject == null)
                return;

            var rect = rowObject.transform as RectTransform;
            var canvasGroup = rowObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                WarnMissingCanvasGroupOnce(rowObject, "MaterialRow");
                return;
            }

            if (rect == null)
                return;

            float delay = index * 0.05f;
            Vector2 basePosition = rect.anchoredPosition;
            rect.DOKill();
            canvasGroup.DOKill();
            rect.anchoredPosition = basePosition + Vector2.right * RowSlideDistance;
            canvasGroup.alpha = 0f;

            DOTween
                .To(
                    () => rect.anchoredPosition,
                    value => rect.anchoredPosition = value,
                    basePosition,
                    0.18f
                )
                .SetEase(Ease.OutQuad)
                .SetDelay(delay)
                .SetUpdate(true);
            DOTween
                .To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, FadeDuration)
                .SetDelay(delay)
                .SetUpdate(true);
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

        private static void WarnMissingCanvasGroupOnce(GameObject target, string uiName)
        {
            if (target == null || !WarnedMissingCanvasGroups.Add(target))
                return;

            Debug.LogWarning(
                $"{nameof(CraftUIAnimationUtility)}: {uiName} '{target.name}' に {nameof(CanvasGroup)} がありません。PrefabまたはScene上で追加してください。",
                target
            );
        }
    }
}
