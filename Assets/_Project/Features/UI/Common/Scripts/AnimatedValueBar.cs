using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 0～最大値を左端基準のバーとして表示する汎用コンポーネント。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatedValueBar : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _fill;

        [SerializeField, Min(0f)]
        private float _openDuration = 0.8f;

        [SerializeField, Min(0f)]
        private float _openDelay = 0.08f;

        [SerializeField, Min(0f)]
        private float _changeDuration = 0.18f;

        [SerializeField]
        private Ease _openEase = Ease.OutCubic;

        private Tween _tween;
        private float _normalizedValue = 1f;

        public float NormalizedValue => _normalizedValue;

        private void Awake()
        {
            ResolveFill();
            EnsureLeftPivot();
        }

        private void OnDisable()
        {
            KillTween();
        }

        public void SetValue(float current, float maximum, bool animate = true)
        {
            _normalizedValue = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            AnimateTo(_normalizedValue, animate ? _changeDuration : 0f, Ease.OutCubic);
        }

        public void PlayOpenAnimation()
        {
            ResolveFill();
            if (_fill == null)
                return;

            KillTween();
            SetScaleX(0f);

            var sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            if (_openDelay > 0f)
                sequence.AppendInterval(_openDelay);
            sequence.Append(_fill.DOScaleX(_normalizedValue, _openDuration).SetEase(_openEase));
            _tween = sequence.OnComplete(() => _tween = null);
        }

        private void AnimateTo(float target, float duration, Ease ease)
        {
            ResolveFill();
            if (_fill == null)
                return;

            KillTween();
            if (duration <= 0f || !isActiveAndEnabled)
            {
                SetScaleX(target);
                return;
            }

            _tween = _fill
                .DOScaleX(target, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() => _tween = null);
        }

        private void SetScaleX(float value)
        {
            Vector3 scale = _fill.localScale;
            scale.x = value;
            _fill.localScale = scale;
        }

        private void ResolveFill()
        {
            if (_fill == null)
                _fill = transform.Find("Background/Fill") as RectTransform;
        }

        private void EnsureLeftPivot()
        {
            if (_fill == null)
                return;

            Vector2 pivot = _fill.pivot;
            pivot.x = 0f;
            _fill.pivot = pivot;
        }

        private void KillTween()
        {
            if (_tween == null)
                return;

            _tween.Kill();
            _tween = null;
        }
    }
}
