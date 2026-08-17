using System.Collections;
using CreativeAI.UI;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CraftWarningToastView : MonoBehaviour
    {
        private const float ShakeDistance = 12f;
        private const float ShakeDuration = 0.6f;
        private const float ShakeFrequency = 5f;
        private const float FadeDelay = 0.8f;
        private const float FadeDuration = 0.6f;

        [SerializeField]
        private TMP_Text _text;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private RectTransform _rectTransform;

        [Header("Messages")]
        [SerializeField]
        private string _categoryMismatchMessage =
            "\u540C\u3058\u30AB\u30C6\u30B4\u30EA\u30FC\u306E\u7D20\u6750\u3092\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044";

        [SerializeField]
        private string _equippedMaterialMessage =
            "\u88C5\u5099\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093";

        [SerializeField]
        private string _missingMaterialsMessage =
            "\u7D20\u6750\u304C\u8DB3\u308A\u307E\u305B\u3093\uFF01";

        [SerializeField]
        private string _quickFoodMaterialMessage =
            "\u5373\u6642\u4F7F\u7528\u306B\u30BB\u30C3\u30C8\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093";

        private Vector2 _basePosition;
        private Vector3 _baseScale;
        private bool _hasBaseTransform;
        private Coroutine _routine;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _text ??= GetComponent<TMP_Text>();
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _rectTransform ??= transform as RectTransform;
        }
#endif

        private void Awake()
        {
            CaptureBaseTransform();
            RestoreHiddenState();
        }

        private void OnDisable()
        {
            StopAnimation();
            RestoreHiddenState();
        }

        public void Show(CraftWarningKind kind)
        {
            Show(GetMessage(kind));
        }

        public void Show(string message)
        {
            if (_text == null || _canvasGroup == null || _rectTransform == null)
                return;

            CaptureBaseTransform();
            StopAnimation();
            RestoreTransform();

            _text.text = message ?? string.Empty;
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _routine = StartCoroutine(PlayRoutine());
        }

        private string GetMessage(CraftWarningKind kind)
        {
            return kind switch
            {
                CraftWarningKind.CategoryMismatch => _categoryMismatchMessage,
                CraftWarningKind.EquippedMaterial => _equippedMaterialMessage,
                CraftWarningKind.MissingMaterials => _missingMaterialsMessage,
                CraftWarningKind.QuickFoodMaterial => string.IsNullOrEmpty(
                    _quickFoodMaterialMessage
                )
                    ? "\u5373\u6642\u4F7F\u7528\u306B\u30BB\u30C3\u30C8\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093"
                    : _quickFoodMaterialMessage,
                _ => string.Empty,
            };
        }

        public void HideImmediate()
        {
            StopAnimation();
            RestoreHiddenState();
            gameObject.SetActive(false);
        }

        private IEnumerator PlayRoutine()
        {
            float elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                float progress = Mathf.Clamp01(elapsed / ShakeDuration);
                float damping = 1f - progress;
                float offsetX =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * ShakeFrequency) * ShakeDistance * damping;
                _rectTransform.anchoredPosition = new Vector2(
                    _basePosition.x + offsetX,
                    _basePosition.y
                );

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            RestoreTransform();
            yield return new WaitForSecondsRealtime(FadeDelay);

            elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / FadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _routine = null;
            RestoreHiddenState();
            gameObject.SetActive(false);
        }

        private void StopAnimation()
        {
            if (_routine == null)
                return;

            StopCoroutine(_routine);
            _routine = null;
        }

        private void CaptureBaseTransform()
        {
            if (_hasBaseTransform || _rectTransform == null)
                return;

            _basePosition = _rectTransform.anchoredPosition;
            _baseScale = _rectTransform.localScale;
            _hasBaseTransform = true;
        }

        private void RestoreTransform()
        {
            if (_rectTransform == null)
                return;

            _rectTransform.anchoredPosition = _hasBaseTransform
                ? _basePosition
                : _rectTransform.anchoredPosition;
            _rectTransform.localScale = _hasBaseTransform ? _baseScale : Vector3.one;
        }

        private void RestoreHiddenState()
        {
            RestoreTransform();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }
    }
}
