using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    internal sealed partial class DialogueChoicePresenter
    {
        public IEnumerator AnimateIn()
        {
            foreach (var choice in _spawned)
            {
                if (choice == null)
                    continue;
                var group = choice.GetComponent<CanvasGroup>();
                var rect = choice.transform as RectTransform;
                Vector2 target = rect != null ? rect.anchoredPosition : Vector2.zero;
                Vector2 start = target + Vector2.up * 18f;
                float duration = Mathf.Max(0.01f, _enterDuration);
                for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    if (group != null)
                        group.alpha = t;
                    if (rect != null)
                        rect.anchoredPosition = Vector2.Lerp(start, target, t);
                    yield return null;
                }
                if (group != null)
                    group.alpha = 1f;
                if (rect != null)
                    rect.anchoredPosition = target;
                if (_staggerDelay > 0f)
                    yield return new WaitForSecondsRealtime(_staggerDelay);
            }
        }

        public IEnumerator AnimateSelection(Button selected)
        {
            foreach (var choice in _spawned)
            {
                var button = choice != null ? choice.GetComponent<Button>() : null;
                if (button != null)
                    button.interactable = false;
            }

            float duration = Mathf.Max(0.01f, _confirmDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = elapsed / duration;
                foreach (var choice in _spawned)
                {
                    if (choice == null)
                        continue;
                    var group = choice.GetComponent<CanvasGroup>();
                    var button = choice.GetComponent<Button>();
                    if (group != null)
                        group.alpha = button == selected ? 1f : Mathf.Lerp(1f, 0.25f, t);
                    if (button == selected)
                        choice.transform.localScale =
                            Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
                }
                yield return null;
            }
        }
    }
}
