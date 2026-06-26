using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    [RequireComponent(typeof(RectTransform))]
    public class HoverScaleOnPointer
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        private static readonly Dictionary<string, HoverScaleOnPointer> _lockedInstances = new();

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
        private Tween _currentTween;
        private Tween _bounceTween;
        private Vector3 _baseLocalPosition;
        private Vector2 _bounceTargetBaseAnchoredPosition;
        private bool _isLocked;

        public void SetTarget(RectTransform target)
        {
            if (_targetRect == target)
                return;

            StopBounce();
            _targetRect = target;
            CacheBasePosition();
        }

        public void SetGroup(string group) => _group = group;

        public void SetHoverScale(float scale) => _hoverScale = Mathf.Max(1f, scale);

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

        public void SetReleaseLockOnOutsideClick(bool release) =>
            _releaseLockOnOutsideClick = release;

        private void Awake()
        {
            if (_targetRect == null)
                _targetRect = GetComponent<RectTransform>();

            CacheBasePosition();
        }

        private void Update()
        {
            if (
                !_releaseLockOnOutsideClick
                || !_isLocked
                || Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame
                || IsPointerOverSelf()
            )
                return;

            ReleaseLockedState();
        }

        private void OnDisable()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            _currentTween?.Kill();
            _currentTween = null;
            StopBounce();

            if (_targetRect != null)
                _targetRect.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
            _bounceTween?.Kill();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isLocked)
                return;
            StartScale(Vector3.one * _hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isLocked)
                return;
            StartScale(Vector3.one);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_lockEnabled)
                return;
            LockSelection();
        }

        private void LockSelection()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current != this)
                current.ReleaseLockedState();

            _lockedInstances[_group] = this;
            _isLocked = true;
            StartScale(Vector3.one * _hoverScale);
            StartBounce();
        }

        private void ReleaseLockedState()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            StartScale(Vector3.one);
            StopBounce();
        }

        public static HoverScaleOnPointer GetLockedInstance(string group) =>
            _lockedInstances.TryGetValue(group, out var instance) ? instance : null;

        public bool IsLocked() => _isLocked;

        public void AcquireLock()
        {
            if (!_lockEnabled)
                return;
            LockSelection();
        }

        public void ReleaseLock() => ReleaseLockedState();

        private void StartScale(Vector3 target)
        {
            if (_targetRect == null)
                return;

            _currentTween?.Kill();
            _currentTween = _targetRect
                .DOScale(target, _animationDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => _currentTween = null);
        }

        private void CacheBasePosition()
        {
            if (_targetRect != null)
                _baseLocalPosition = _targetRect.localPosition;
            if (_bounceTarget != null)
                _bounceTargetBaseAnchoredPosition = _bounceTarget.anchoredPosition;
        }

        private void StartBounce()
        {
            if (_targetRect == null || _bounceHeight <= 0f || _bounceDuration <= 0f)
                return;

            StopBounce();
            CacheBasePosition();
            float direction = GetBounceDirection();

            var sequence = DOTween.Sequence();
            sequence.Append(
                DOTween.To(
                    () => _targetRect.localPosition.y,
                    y =>
                    {
                        var position = _targetRect.localPosition;
                        position.y = y;
                        _targetRect.localPosition = position;
                    },
                    _baseLocalPosition.y + _bounceHeight * direction,
                    _bounceDuration
                )
            );

            if (_bounceTarget != null)
            {
                sequence.Join(
                    DOTween.To(
                        () => _bounceTarget.anchoredPosition.y,
                        y =>
                        {
                            var position = _bounceTarget.anchoredPosition;
                            position.y = y;
                            _bounceTarget.anchoredPosition = position;
                        },
                        _bounceTargetBaseAnchoredPosition.y + _bounceHeight * direction,
                        _bounceDuration
                    )
                );
            }

            _bounceTween = sequence
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopBounce()
        {
            bool wasBouncing = _bounceTween != null;
            _bounceTween?.Kill();
            _bounceTween = null;

            // Layout計算前に保存した古い座標を、未再生時に書き戻さない。
            if (!wasBouncing || _targetRect == null)
                return;

            _targetRect.localPosition = _baseLocalPosition;
            if (_bounceTarget != null)
                _bounceTarget.anchoredPosition = _bounceTargetBaseAnchoredPosition;
        }

        private float GetBounceDirection()
        {
            var mask = _targetRect.GetComponentInParent<RectMask2D>();
            if (mask == null)
                return 1f;

            var grid = _targetRect.GetComponentInParent<GridLayoutGroup>();
            if (IsInFirstGridRow(grid))
                return -1f;

            // Programmatic selection can happen before the inventory grid has completed
            // its first layout pass.
            Canvas.ForceUpdateCanvases();
            if (grid != null && grid.transform is RectTransform gridRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            _targetRect.ForceUpdateRectTransforms();

            var targetCorners = new Vector3[4];
            var maskCorners = new Vector3[4];
            _targetRect.GetWorldCorners(targetCorners);
            mask.rectTransform.GetWorldCorners(maskCorners);
            float scaledBounceHeight = _bounceHeight * _targetRect.lossyScale.y;

            return targetCorners[1].y + scaledBounceHeight > maskCorners[1].y ? -1f : 1f;
        }

        private bool IsInFirstGridRow(GridLayoutGroup grid)
        {
            if (grid == null)
                return false;

            Transform slot = _targetRect;
            while (slot.parent != null && slot.parent != grid.transform)
                slot = slot.parent;

            if (slot.parent != grid.transform)
                return false;

            int columnCount = grid.constraint switch
            {
                GridLayoutGroup.Constraint.FixedColumnCount => grid.constraintCount,
                GridLayoutGroup.Constraint.FixedRowCount => Mathf.CeilToInt(
                    (float)grid.transform.childCount / grid.constraintCount
                ),
                _ => Mathf.Max(
                    1,
                    Mathf.FloorToInt(
                        (
                            ((RectTransform)grid.transform).rect.width
                            - grid.padding.horizontal
                            + grid.spacing.x
                        ) / (grid.cellSize.x + grid.spacing.x)
                    )
                ),
            };

            return slot.GetSiblingIndex() < columnCount;
        }

        private bool IsPointerOverSelf()
        {
            if (EventSystem.current == null || Mouse.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue(),
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                if (
                    result.gameObject == gameObject
                    || result.gameObject.transform.IsChildOf(transform)
                )
                    return true;
            }

            return false;
        }
    }
}
