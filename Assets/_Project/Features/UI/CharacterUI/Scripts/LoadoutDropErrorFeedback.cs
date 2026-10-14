using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class LoadoutDropErrorFeedback : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Color _errorColor = new Color32(0xff, 0x55, 0x55, 0xff);

        [SerializeField, Min(0f)]
        private float _verticalOffset = 18f;

        [SerializeField, Min(0f)]
        private float _fadeInDuration = 0.08f;

        [SerializeField, Min(0f)]
        private float _blinkDuration = 0.1f;

        [SerializeField, Min(0f)]
        private float _visibleDuration = 0.65f;

        [SerializeField, Min(0f)]
        private float _fadeOutDuration = 0.18f;

        [SerializeField, Range(0f, 1f)]
        private float _blinkAlpha = 0.25f;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Sequence _sequence;

        private void Awake()
        {
            ResolveReferences();
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            KillSequence();
        }

        public void Show(string message, RectTransform target)
        {
            ResolveReferences();
            if (_label == null || _canvasGroup == null)
                return;

            KillSequence();
            PositionAbove(target);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            _label.text = message;
            _label.color = _errorColor;
            _canvasGroup.alpha = 0f;

            _sequence = DOTween
                .Sequence()
                .SetUpdate(true)
                .Append(_canvasGroup.DOFade(1f, _fadeInDuration))
                .Append(_canvasGroup.DOFade(_blinkAlpha, _blinkDuration))
                .Append(_canvasGroup.DOFade(1f, _blinkDuration))
                .Append(_canvasGroup.DOFade(_blinkAlpha, _blinkDuration))
                .Append(_canvasGroup.DOFade(1f, _blinkDuration))
                .AppendInterval(_visibleDuration)
                .Append(_canvasGroup.DOFade(0f, _fadeOutDuration))
                .OnComplete(() =>
                {
                    _sequence = null;
                    gameObject.SetActive(false);
                });
        }

        private void PositionAbove(RectTransform target)
        {
            if (target == null || _rectTransform?.parent is not RectTransform parent)
                return;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldTopCenter = (corners[1] + corners[2]) * 0.5f;
            Vector2 localPosition = parent.InverseTransformPoint(worldTopCenter);
            localPosition.y += _verticalOffset;

            float halfWidth = _rectTransform.rect.width * 0.5f;
            localPosition.x = Mathf.Clamp(
                localPosition.x,
                parent.rect.xMin + halfWidth,
                parent.rect.xMax - halfWidth
            );
            _rectTransform.anchoredPosition = localPosition;
        }

        private void ResolveReferences()
        {
            _rectTransform ??= transform as RectTransform;
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _label ??= GetComponent<TMP_Text>();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void KillSequence()
        {
            _sequence?.Kill();
            _sequence = null;
        }
    }
}
