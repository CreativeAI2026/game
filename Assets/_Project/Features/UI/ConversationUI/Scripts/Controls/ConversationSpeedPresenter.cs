using System.Collections;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>テキスト速度のラベルと一時通知を表示する。</summary>
    internal sealed class ConversationSpeedPresenter
    {
        private readonly MonoBehaviour _owner;
        private TMP_Text _controlLabel;
        private TMP_Text _toast;
        private Coroutine _toastRoutine;

        public ConversationSpeedPresenter(MonoBehaviour owner) => _owner = owner;

        public void Configure(TMP_Text controlLabel, TMP_Text toast)
        {
            _controlLabel = controlLabel;
            _toast = toast;
        }

        public void SetSpeed(ConversationView.TextSpeed speed, bool showToast)
        {
            if (_controlLabel != null)
                _controlLabel.text = GetControlLabel(speed);
            if (showToast)
                ShowToast(speed);
        }

        public void Cancel()
        {
            if (_toastRoutine != null)
            {
                _owner.StopCoroutine(_toastRoutine);
                _toastRoutine = null;
            }
            if (_toast != null && _toast.TryGetComponent(out CanvasGroup group))
                group.alpha = 0f;
        }

        private void ShowToast(ConversationView.TextSpeed speed)
        {
            if (_toast == null || !Application.isPlaying)
                return;
            if (_toastRoutine != null)
                _owner.StopCoroutine(_toastRoutine);
            _toast.text = speed switch
            {
                ConversationView.TextSpeed.Slow => "TEXT SPEED  x0.6",
                ConversationView.TextSpeed.Fast => "TEXT SPEED  x2",
                ConversationView.TextSpeed.Instant => "TEXT SPEED  MAX",
                _ => "TEXT SPEED  x1",
            };
            _toastRoutine = _owner.StartCoroutine(AnimateToast());
        }

        private IEnumerator AnimateToast()
        {
            var group = _toast.GetComponent<CanvasGroup>();
            var rect = _toast.rectTransform;
            Vector2 basePosition = rect.anchoredPosition;
            const float duration = 0.7f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float normalized = elapsed / duration;
                group.alpha =
                    normalized < 0.18f
                        ? normalized / 0.18f
                        : 1f - Mathf.InverseLerp(0.62f, 1f, normalized);
                rect.anchoredPosition = basePosition + Vector2.up * (7f * normalized);
                yield return null;
            }
            group.alpha = 0f;
            rect.anchoredPosition = basePosition;
            _toastRoutine = null;
        }

        private static string GetControlLabel(ConversationView.TextSpeed speed) =>
            speed switch
            {
                ConversationView.TextSpeed.Slow => "SPEED x0.6",
                ConversationView.TextSpeed.Fast => "SPEED x2",
                ConversationView.TextSpeed.Instant => "SPEED MAX",
                _ => "SPEED x1",
            };
    }
}
