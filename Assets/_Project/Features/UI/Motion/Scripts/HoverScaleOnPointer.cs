using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ポインタを乗せると対象を拡大し、クリックすると拡大したまま固定する(同じグループで固定できるのは1つ)。
    /// 固定中は対象を上下に揺らせる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HoverScaleOnPointer
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

        [SerializeField]
        private bool _hoverScaleEnabled = true;

        [Header("Selected Bounce")]
        [SerializeField]
        private bool _bounceEnabled = true;

        [SerializeField]
        private float _bounceHeight = 8f;

        [SerializeField]
        private float _bounceDuration = 0.8f;

        [SerializeField]
        private RectTransform _targetRect; // 拡大・揺れの対象

        private Tween _currentTween;
        private Tween _bounceTween;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _isLocked;
        private bool _bounceAllowed = true;

        // 揺れる向きを決める時の四隅。毎回作らずに使い回す
        private static readonly Vector3[] s_targetCorners = new Vector3[4];
        private static readonly Vector3[] s_maskCorners = new Vector3[4];

        private void Awake()
        {
            _baseLocalScale = _targetRect.localScale;
            _baseLocalPosition = _targetRect.localPosition;
        }

        private void OnDestroy()
        {
            _currentTween?.Kill();
            _bounceTween?.Kill();
        }

        /// <summary>固定のグループを変える。同じグループで同時に固定できるのは1つ。</summary>
        public void SetGroup(string group) => _group = group;

        /// <summary>固定中に揺らしてよいかを切り替える。</summary>
        public void SetBounceAllowed(bool allowed)
        {
            _bounceAllowed = allowed;
            if (!_bounceAllowed)
                StopBounce();
        }

        /// <summary>外側をクリックしたら固定を外すかを切り替える。</summary>
        public void SetReleaseLockOnOutsideClick(bool release) =>
            _releaseLockOnOutsideClick = release;

        private void StartScale(float scaleMultiplier)
        {
            if (!_hoverScaleEnabled || _targetRect == null)
                return;

            _currentTween?.Kill();
            // 対象の DOKill で止まらないよう、Sequence に包んで持つ
            _currentTween = DOTween
                .Sequence()
                .SetUpdate(true)
                .Join(
                    _targetRect
                        .DOScale(_baseLocalScale * scaleMultiplier, _animationDuration)
                        .SetEase(Ease.OutQuad)
                )
                .OnComplete(() => _currentTween = null);
        }

        private void RestoreBaseScale() => _targetRect.localScale = _baseLocalScale;

        private void StartBounce()
        {
            if (
                !_bounceEnabled
                || !_bounceAllowed
                || _targetRect == null
                || _bounceHeight <= 0f
                || _bounceDuration <= 0f
            )
                return;

            StopBounce();
            RebuildOwningGridLayout();
            _baseLocalPosition = _targetRect.localPosition;
            float direction = GetBounceDirection();

            _bounceTween = DOTween
                .To(
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
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopBounce()
        {
            bool wasBouncing = _bounceTween != null;
            _bounceTween?.Kill();
            _bounceTween = null;

            if (!wasBouncing || _targetRect == null)
                return;

            if (!RebuildOwningGridLayout())
                _targetRect.localPosition = _baseLocalPosition;
        }

        private bool RebuildOwningGridLayout()
        {
            var grid =
                _targetRect != null ? _targetRect.GetComponentInParent<GridLayoutGroup>() : null;
            if (grid == null || grid.transform is not RectTransform gridRect)
                return false;

            // 全 Canvas を強制更新せず、この欄のグリッドだけ並べ直す
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            _targetRect.ForceUpdateRectTransforms();
            return _targetRect.parent == grid.transform;
        }

        private float GetBounceDirection()
        {
            var mask = _targetRect.GetComponentInParent<RectMask2D>();
            if (mask == null)
                return 1f;

            var grid = _targetRect.GetComponentInParent<GridLayoutGroup>();
            if (IsInFirstGridRow(grid))
                return -1f;

            // グリッドの並べ直しは呼ぶ側(StartBounce)で済ませている
            _targetRect.GetWorldCorners(s_targetCorners);
            mask.rectTransform.GetWorldCorners(s_maskCorners);
            float scaledBounceHeight = _bounceHeight * _targetRect.lossyScale.y;

            return s_targetCorners[1].y + scaledBounceHeight > s_maskCorners[1].y ? -1f : 1f;
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

        private static readonly Dictionary<string, HoverScaleOnPointer> _lockedInstances = new();

        /// <summary>拡大したまま固定しているか。</summary>
        public bool IsLocked() => _isLocked;

        /// <summary>コードから固定する(選択中の表示)。固定できない設定なら何もしない。</summary>
        public void AcquireLock()
        {
            if (!_lockEnabled)
                return;

            LockSelection();
        }

        /// <summary>固定を外して元の大きさへ戻す。</summary>
        public void ReleaseLock() => ReleaseLockedState();

        private void OnDisable()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            _currentTween?.Kill();
            _currentTween = null;
            StopBounce();

            if (_hoverScaleEnabled)
                RestoreBaseScale();
        }

        private void LockSelection()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current != this)
                current.ReleaseLockedState();

            _lockedInstances[_group] = this;
            _isLocked = true;
            StartScale(_hoverScale);
            StartBounce();
        }

        private void ReleaseLockedState()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            StartScale(1f);
            StopBounce();
        }

        private void Update()
        {
            if (
                !_releaseLockOnOutsideClick
                || !_isLocked
                || !UIInputActionMap.WasClickPressedThisFrame()
                || IsPointerOverSelf()
            )
                return;

            ReleaseLockedState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isLocked)
                return;

            StartScale(_hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isLocked)
                return;

            StartScale(1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_lockEnabled)
                return;

            LockSelection();
        }

        private bool IsPointerOverSelf()
        {
            if (
                EventSystem.current == null
                || !UIInputActionMap.TryReadPointerPosition(out Vector2 pointerPosition)
            )
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = pointerPosition,
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
