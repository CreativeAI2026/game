using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話ウィンドウ、操作ガイド、AUTO表示と送りインジケーターを担当する。</summary>
    internal sealed class ConversationChromePresenter
    {
        private readonly MonoBehaviour _owner;
        private CanvasGroup _root;
        private RectTransform _window;
        private TMP_Text _fontSource;
        private GameObject _nextIndicator;
        private TMP_Text _autoIndicator;
        private TMP_Text _controlGuide;
        private Image _autoProgress;
        private RectTransform _autoProgressRect;
        private Coroutine _bounceCoroutine;
        private Vector2 _indicatorBasePosition;
        private bool _hasIndicatorBasePosition;
        private Vector2 _windowBasePosition;
        private bool _hasWindowBasePosition;
        private float _bounceHeight;
        private float _bounceDuration;
        private float _enterDuration;
        private float _enterOffsetY;
        private string _autoLabel;
        private float _autoWaveStartTime;
        private Coroutine _lineTextAnimation;
        private CanvasGroup _nameGroup;
        private CanvasGroup _bodyGroup;
        private Vector2 _nameBasePosition;
        private Vector2 _bodyBasePosition;
        private bool _hasLineTextPositions;

        public ConversationChromePresenter(MonoBehaviour owner) => _owner = owner;

        public TMP_Text AutoIndicator => _autoIndicator;
        public TMP_Text ControlGuide => _controlGuide;
        public Image AutoProgress => _autoProgress;

        public void Configure(
            CanvasGroup root,
            RectTransform window,
            TMP_Text fontSource,
            GameObject nextIndicator,
            TMP_Text autoIndicator,
            TMP_Text controlGuide,
            Image autoProgress,
            float bounceHeight,
            float bounceDuration,
            float enterDuration,
            float enterOffsetY,
            string autoLabel
        )
        {
            _root = root;
            _window = window;
            _fontSource = fontSource;
            _nextIndicator = nextIndicator;
            _autoIndicator = autoIndicator;
            _controlGuide = controlGuide;
            _autoProgress = autoProgress;
            _autoProgressRect = autoProgress != null ? autoProgress.rectTransform : null;
            _bounceHeight = bounceHeight;
            _bounceDuration = bounceDuration;
            _enterDuration = enterDuration;
            _enterOffsetY = enterOffsetY;
            _autoLabel = autoLabel;
        }

        public void EnsureView()
        {
            EnsureAutoIndicator();
            EnsureAutoProgress();
            EnsureControlGuide();
        }

        public void Tick(bool autoMode, float progress)
        {
            if (_autoProgress != null)
            {
                SetAutoProgress(progress);
                _autoProgress.gameObject.SetActive(autoMode);
            }
            if (_autoIndicator == null || !autoMode)
                return;
            Color color = _autoIndicator.color;
            color.a = 0.72f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.18f;
            _autoIndicator.color = color;
            AnimateAutoText();
        }

        public void SetAutoMode(bool enabled)
        {
            EnsureView();
            if (_autoIndicator == null)
                return;
            if (enabled && !_autoIndicator.gameObject.activeSelf)
                _autoWaveStartTime = Time.unscaledTime;
            _autoIndicator.text = _autoLabel;
            _autoIndicator.gameObject.SetActive(enabled);
        }

        public void SetWindowHidden(bool hidden)
        {
            if (_window != null)
                _window.gameObject.SetActive(!hidden);
            if (_controlGuide != null)
                _controlGuide.text = hidden ? "WINDOW   H" : DefaultGuide;
        }

        public void SetTextSpeed(ConversationView.TextSpeed speed)
        {
            if (_controlGuide != null)
                _controlGuide.text = $"NEXT   ENTER / SPACE     SPEED  {speed}";
        }

        public void SetChoiceGuide(bool choicesVisible)
        {
            if (_controlGuide != null)
                _controlGuide.text = choicesVisible
                    ? "SELECT   UP / DOWN     CONFIRM   ENTER"
                    : DefaultGuide;
        }

        public void PrepareLineText(TMP_Text name, TMP_Text body)
        {
            if (_lineTextAnimation != null)
            {
                _owner.StopCoroutine(_lineTextAnimation);
                _lineTextAnimation = null;
            }
            if (name == null || body == null)
                return;
            _nameGroup = name.GetComponent<CanvasGroup>();
            if (_nameGroup == null)
                _nameGroup = name.gameObject.AddComponent<CanvasGroup>();
            _bodyGroup = body.GetComponent<CanvasGroup>();
            if (_bodyGroup == null)
                _bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
            if (!_hasLineTextPositions)
            {
                _nameBasePosition = name.rectTransform.anchoredPosition;
                _bodyBasePosition = body.rectTransform.anchoredPosition;
                _hasLineTextPositions = true;
            }
            _nameGroup.alpha = 0f;
            _bodyGroup.alpha = 0f;
            name.rectTransform.anchoredPosition = _nameBasePosition + Vector2.down * 12f;
            body.rectTransform.anchoredPosition = _bodyBasePosition;
        }

        public void PlayLineTextEntrance(TMP_Text name, TMP_Text body, bool showName)
        {
            if (name == null || body == null || _nameGroup == null || _bodyGroup == null)
                return;
            name.gameObject.SetActive(showName);
            if (!Application.isPlaying)
            {
                _nameGroup.alpha = showName ? 1f : 0f;
                _bodyGroup.alpha = 1f;
                name.rectTransform.anchoredPosition = _nameBasePosition;
                body.rectTransform.anchoredPosition = _bodyBasePosition;
                return;
            }
            _lineTextAnimation = _owner.StartCoroutine(AnimateLineText(name, body, showName));
        }

        public void StartBounce()
        {
            StopBounce();
            if (_nextIndicator == null || _nextIndicator.transform is not RectTransform rect)
                return;
            _indicatorBasePosition = rect.anchoredPosition;
            _hasIndicatorBasePosition = true;
            _nextIndicator.SetActive(true);
            _bounceCoroutine = _owner.StartCoroutine(Bounce(rect));
        }

        public void StopBounce()
        {
            if (_bounceCoroutine != null)
            {
                _owner.StopCoroutine(_bounceCoroutine);
                _bounceCoroutine = null;
            }
            if (
                _hasIndicatorBasePosition
                && _nextIndicator != null
                && _nextIndicator.transform is RectTransform rect
            )
                rect.anchoredPosition = _indicatorBasePosition;
            _hasIndicatorBasePosition = false;
            if (_nextIndicator != null)
                _nextIndicator.SetActive(false);
        }

        public IEnumerator Show()
        {
            if (_root == null)
                yield break;
            _root.interactable = false;
            _root.blocksRaycasts = true;
            if (_root.alpha >= 0.999f)
            {
                _root.interactable = true;
                yield break;
            }
            CaptureWindowPosition();
            float duration = Mathf.Max(0.01f, _enterDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _root.alpha = t;
                if (_window != null && _hasWindowBasePosition)
                    _window.anchoredPosition =
                        _windowBasePosition + Vector2.up * Mathf.Lerp(_enterOffsetY, 0f, t);
                yield return null;
            }
            _root.alpha = 1f;
            _root.interactable = true;
            RestoreWindowPosition();
        }

        public IEnumerator Hide(float duration)
        {
            StopBounce();
            if (_root == null)
                yield break;
            float start = _root.alpha;
            duration = Mathf.Max(0.01f, duration);
            _root.interactable = false;
            _root.blocksRaycasts = false;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                _root.alpha = Mathf.Lerp(start, 0f, elapsed / duration);
                yield return null;
            }
            _root.alpha = 0f;
        }

        public IEnumerator HideLineText(float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            float nameStart = _nameGroup != null ? _nameGroup.alpha : 0f;
            float bodyStart = _bodyGroup != null ? _bodyGroup.alpha : 0f;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_bodyGroup != null)
                    _bodyGroup.alpha = Mathf.Lerp(bodyStart, 0f, t);
                if (_nameGroup != null)
                    _nameGroup.alpha = Mathf.Lerp(nameStart, 0f, Mathf.Clamp01(t * 1.2f));
                yield return null;
            }
            if (_bodyGroup != null)
                _bodyGroup.alpha = 0f;
            if (_nameGroup != null)
                _nameGroup.alpha = 0f;
        }

        public void HideImmediate()
        {
            StopBounce();
            if (_root != null)
            {
                _root.alpha = 0f;
                _root.interactable = false;
                _root.blocksRaycasts = false;
            }
            RestoreWindowPosition();
        }

        private IEnumerator Bounce(RectTransform rect)
        {
            for (float elapsed = 0f; ; elapsed += Time.unscaledDeltaTime)
            {
                float phase = Mathf.Repeat(elapsed / Mathf.Max(0.01f, _bounceDuration), 1f);
                rect.anchoredPosition =
                    _indicatorBasePosition
                    + Vector2.up * (Mathf.Sin(phase * Mathf.PI) * _bounceHeight);
                yield return null;
            }
        }

        private IEnumerator AnimateLineText(TMP_Text name, TMP_Text body, bool showName)
        {
            const float duration = 0.24f;
            const float bodyDelay = 0.06f;
            for (float elapsed = 0f; elapsed < duration + bodyDelay; elapsed += FrameDelta())
            {
                float nameT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float bodyT = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((elapsed - bodyDelay) / duration)
                );
                _nameGroup.alpha = showName ? nameT : 0f;
                _bodyGroup.alpha = bodyT;
                name.rectTransform.anchoredPosition = Vector2.Lerp(
                    _nameBasePosition + Vector2.down * 12f,
                    _nameBasePosition,
                    nameT
                );
                yield return null;
            }
            _nameGroup.alpha = showName ? 1f : 0f;
            _bodyGroup.alpha = 1f;
            name.rectTransform.anchoredPosition = _nameBasePosition;
            _lineTextAnimation = null;
        }

        private void EnsureAutoIndicator()
        {
            if (_autoIndicator != null || _root == null)
                return;
            var target = new GameObject("AutoModeIndicator", typeof(RectTransform));
            target.transform.SetParent(_root.transform, false);
            target.transform.SetAsLastSibling();
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-36f, -28f);
            rect.sizeDelta = new Vector2(180f, 52f);
            _autoIndicator = target.AddComponent<TextMeshProUGUI>();
            _autoIndicator.alignment = TextAlignmentOptions.Center;
            _autoIndicator.fontSize = 28f;
            _autoIndicator.fontStyle = FontStyles.Bold;
            _autoIndicator.color = new Color(0.75f, 0.9f, 1f, 1f);
            _autoIndicator.raycastTarget = false;
            _autoIndicator.font = _fontSource != null ? _fontSource.font : null;
        }

        private void EnsureControlGuide()
        {
            if (_controlGuide != null || _root == null)
                return;
            var target = new GameObject("ControlGuide", typeof(RectTransform));
            target.transform.SetParent(_root.transform, false);
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(26f, 20f);
            rect.sizeDelta = new Vector2(520f, 38f);
            _controlGuide = target.AddComponent<TextMeshProUGUI>();
            _controlGuide.text = DefaultGuide;
            _controlGuide.fontSize = 20f;
            _controlGuide.color = new Color(1f, 1f, 1f, 0.62f);
            _controlGuide.alignment = TextAlignmentOptions.BottomLeft;
            _controlGuide.raycastTarget = false;
            _controlGuide.font = _fontSource != null ? _fontSource.font : null;
        }

        private void EnsureAutoProgress()
        {
            if (_autoProgress != null || _autoIndicator == null)
                return;
            var progress = new GameObject("AutoProgress", typeof(RectTransform));
            progress.transform.SetParent(_autoIndicator.transform, false);
            var background = progress.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.3f);
            background.raycastTarget = false;
            var rect = progress.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0f);
            rect.anchorMax = new Vector2(0.9f, 0f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(0f, 6f);
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(progress.transform, false);
            _autoProgress = fill.AddComponent<Image>();
            _autoProgress.color = new Color(0.5f, 0.85f, 1f, 1f);
            _autoProgress.type = Image.Type.Simple;
            _autoProgress.raycastTarget = false;
            _autoProgressRect = fill.GetComponent<RectTransform>();
            SetAutoProgress(0f);
        }

        private void SetAutoProgress(float progress)
        {
            if (_autoProgressRect == null)
                return;

            float amount = Mathf.Clamp01(progress);
            _autoProgressRect.anchorMin = Vector2.zero;
            _autoProgressRect.anchorMax = new Vector2(amount, 1f);
            _autoProgressRect.pivot = new Vector2(0f, 0.5f);
            _autoProgressRect.offsetMin = Vector2.zero;
            _autoProgressRect.offsetMax = Vector2.zero;
        }

        private void AnimateAutoText()
        {
            _autoIndicator.ForceMeshUpdate();
            var textInfo = _autoIndicator.textInfo;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                const float characterDuration = 0.42f;
                const float characterDelay = 0.11f;
                const float pauseDuration = 1.5f;
                float waveDuration =
                    characterDuration + characterDelay * (textInfo.characterCount - 1);
                float cycle = Mathf.Repeat(
                    Time.unscaledTime - _autoWaveStartTime,
                    waveDuration + pauseDuration
                );
                float characterTime = cycle - i * characterDelay;
                float offsetY =
                    characterTime >= 0f && characterTime <= characterDuration
                        ? Mathf.Sin(characterTime / characterDuration * Mathf.PI) * 5f
                        : 0f;
                int vertexIndex = character.vertexIndex;
                var vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
                var offset = Vector3.up * offsetY;
                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                var meshInfo = textInfo.meshInfo[i];
                meshInfo.mesh.vertices = meshInfo.vertices;
                _autoIndicator.UpdateGeometry(meshInfo.mesh, i);
            }
        }

        private void CaptureWindowPosition()
        {
            if (_window == null || _hasWindowBasePosition)
                return;
            _windowBasePosition = _window.anchoredPosition;
            _hasWindowBasePosition = true;
        }

        private void RestoreWindowPosition()
        {
            if (_window != null && _hasWindowBasePosition)
                _window.anchoredPosition = _windowBasePosition;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);

        private const string DefaultGuide = "NEXT   ENTER / SPACE";
    }
}
