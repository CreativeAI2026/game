using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI.InventoryUI
{
    [RequireComponent(typeof(RectTransform))]
    public partial class HoverScaleOnPointer
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        [SerializeField]
        private string _group = "default";

        [SerializeField]
        private bool _lockEnabled = true;

        [SerializeField]
        private bool _releaseLockOnOutsideClick = true;

        [SerializeField]
        private float _hoverScale = 1.2f;

        [SerializeField]
        private float _animationDuration = 0.2f;

        [Header("Selected Bounce")]
        [SerializeField]
        private float _bounceHeight = 8f;

        [SerializeField]
        private float _bounceDuration = 0.8f;

        private RectTransform _targetRect;
        private RectTransform _bounceTarget;
        private readonly List<RectTransform> _linkedTargets = new();
        private readonly List<Vector3> _linkedTargetBaseLocalPositions = new();
        private readonly List<Vector3> _linkedTargetBaseLocalScales = new();
        private readonly Dictionary<RectTransform, Vector3> _cachedBaseLocalScales = new();
        private Tween _currentTween;
        private Tween _bounceTween;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale = Vector3.one;
        private Vector2 _bounceTargetBaseAnchoredPosition;
        private bool _isLocked;

        private void Awake()
        {
            _targetRect ??= GetComponent<RectTransform>();
            CacheBaseScale();
            CacheBasePosition();
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
            _bounceTween?.Kill();
        }

        public void SetTarget(RectTransform target)
        {
            if (_targetRect == target)
                return;

            StopBounce();
            _targetRect = target;
            CacheBaseScale();
            CacheBasePosition();
        }

        public void SetGroup(string group) => _group = group;

        public void SetHoverScale(float scale) => _hoverScale = Mathf.Max(1f, scale);

        public void SetLockEnabled(bool enabled) => _lockEnabled = enabled;

        public void SetBounceHeight(float height)
        {
            _bounceHeight = Mathf.Max(0f, height);
            if (_bounceHeight <= 0f)
                StopBounce();
        }

        public void SetBounceTarget(RectTransform target)
        {
            StopBounce();
            _bounceTarget = target;
            CacheBasePosition();
        }

        public void SetLinkedTargets(params RectTransform[] targets)
        {
            StopBounce();
            _linkedTargets.Clear();

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null || target == _targetRect)
                        continue;

                    if (!_linkedTargets.Contains(target))
                        _linkedTargets.Add(target);
                }
            }

            CacheLinkedTargetBaseScales();
            CacheBasePosition();
        }

        public void SetReleaseLockOnOutsideClick(bool release) =>
            _releaseLockOnOutsideClick = release;

        private void StartScale(float scaleMultiplier)
        {
            if (_targetRect == null)
                return;

            _currentTween?.Kill();
            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(
                _targetRect
                    .DOScale(_baseLocalScale * scaleMultiplier, _animationDuration)
                    .SetEase(Ease.OutQuad)
            );

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                var linkedTarget = _linkedTargets[i];
                if (linkedTarget == null)
                    continue;

                var baseScale =
                    i < _linkedTargetBaseLocalScales.Count
                        ? _linkedTargetBaseLocalScales[i]
                        : linkedTarget.localScale;

                sequence.Join(
                    linkedTarget
                        .DOScale(baseScale * scaleMultiplier, _animationDuration)
                        .SetEase(Ease.OutQuad)
                );
            }

            _currentTween = sequence.OnComplete(() => _currentTween = null);
        }

        private void CacheBasePosition()
        {
            if (_targetRect != null)
                _baseLocalPosition = _targetRect.localPosition;
            if (_bounceTarget != null)
                _bounceTargetBaseAnchoredPosition = _bounceTarget.anchoredPosition;

            _linkedTargetBaseLocalPositions.Clear();
            foreach (var linkedTarget in _linkedTargets)
                _linkedTargetBaseLocalPositions.Add(
                    linkedTarget != null ? linkedTarget.localPosition : Vector3.zero
                );
        }

        private void CacheBaseScale(bool force = false)
        {
            if (_targetRect == null)
                return;

            if (force || !_cachedBaseLocalScales.TryGetValue(_targetRect, out _baseLocalScale))
            {
                _baseLocalScale = _targetRect.localScale;
                _cachedBaseLocalScales[_targetRect] = _baseLocalScale;
            }
        }

        private void CacheLinkedTargetBaseScales()
        {
            _linkedTargetBaseLocalScales.Clear();
            foreach (var linkedTarget in _linkedTargets)
            {
                if (linkedTarget == null)
                {
                    _linkedTargetBaseLocalScales.Add(Vector3.one);
                    continue;
                }

                if (!_cachedBaseLocalScales.TryGetValue(linkedTarget, out var baseScale))
                {
                    baseScale = linkedTarget.localScale;
                    _cachedBaseLocalScales[linkedTarget] = baseScale;
                }

                _linkedTargetBaseLocalScales.Add(baseScale);
            }
        }
    }
}
