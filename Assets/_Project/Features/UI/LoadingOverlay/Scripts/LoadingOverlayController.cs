using System.Collections;
using CreativeAI.Core.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.LoadingOverlay
{
    [RequireComponent(typeof(CanvasGroup))]
    public class LoadingOverlayController : MonoBehaviour, ILoadingOverlay
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private Slider _progressBar;

        private void Reset()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            if (_progressBar != null)
                _progressBar.value = 0f;
        }

        public IEnumerator ShowCoroutine(float duration)
        {
            _canvasGroup.blocksRaycasts = true;
            if (_progressBar != null)
                _progressBar.value = 0f;
            yield return FadeRoutine(_canvasGroup.alpha, 1f, duration);
        }

        public IEnumerator HideCoroutine(float duration)
        {
            yield return FadeRoutine(_canvasGroup.alpha, 0f, duration);
            _canvasGroup.blocksRaycasts = false;
        }

        public void SetProgress(float progress01)
        {
            if (_progressBar != null)
                _progressBar.value = Mathf.Clamp01(progress01);
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = to;
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
