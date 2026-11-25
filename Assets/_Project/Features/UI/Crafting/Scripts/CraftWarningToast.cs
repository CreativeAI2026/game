using System.Collections;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 調合画面の警告トースト。文言を出して横に揺らし、少し待ってからフェードアウトする。
    /// </summary>
    internal sealed class CraftWarningToast
    {
        private const float ShakeDistance = 12f;
        private const float ShakeDuration = 0.6f;
        private const float ShakeFrequency = 5f;
        private const float FadeDelay = 0.8f;
        private const float FadeDuration = 0.6f;

        private readonly MonoBehaviour _host;
        private readonly TMP_Text _text;
        private readonly CanvasGroup _canvasGroup;
        private readonly Vector2 _basePosition;
        private Coroutine _routine;

        /// <param name="host">揺れとフェードのコルーチンを動かす MonoBehaviour</param>
        public CraftWarningToast(MonoBehaviour host, TMP_Text text, CanvasGroup canvasGroup)
        {
            _host = host;
            _text = text;
            _canvasGroup = canvasGroup;
            if (text != null)
                _basePosition = text.rectTransform.anchoredPosition;
        }

        public void Show(string message)
        {
            StopAnimation();
            _text.text = message;
            _text.gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _routine = _host.StartCoroutine(PlayRoutine());
        }

        public void Hide()
        {
            StopAnimation();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (_text != null)
                _text.gameObject.SetActive(false);
        }

        private IEnumerator PlayRoutine()
        {
            var rect = _text.rectTransform;
            float elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                float damping = 1f - Mathf.Clamp01(elapsed / ShakeDuration);
                float offsetX =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * ShakeFrequency) * ShakeDistance * damping;
                rect.anchoredPosition = _basePosition + new Vector2(offsetX, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            rect.anchoredPosition = _basePosition;
            yield return new WaitForSecondsRealtime(FadeDelay);

            elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / FadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _routine = null;
            Hide();
        }

        private void StopAnimation()
        {
            if (_routine != null)
            {
                _host.StopCoroutine(_routine);
                _routine = null;
            }

            if (_text != null)
                _text.rectTransform.anchoredPosition = _basePosition;
        }
    }
}
