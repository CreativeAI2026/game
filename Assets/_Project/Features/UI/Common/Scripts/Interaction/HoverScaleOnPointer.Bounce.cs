using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class HoverScaleOnPointer
    {
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

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                var linkedTarget = _linkedTargets[i];
                if (linkedTarget == null)
                    continue;

                var basePosition =
                    i < _linkedTargetBaseLocalPositions.Count
                        ? _linkedTargetBaseLocalPositions[i]
                        : linkedTarget.localPosition;

                sequence.Join(
                    DOTween.To(
                        () => linkedTarget.localPosition.y,
                        y =>
                        {
                            var position = linkedTarget.localPosition;
                            position.y = y;
                            linkedTarget.localPosition = position;
                        },
                        basePosition.y + _bounceHeight * direction,
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

            if (!wasBouncing || _targetRect == null)
                return;

            _targetRect.localPosition = _baseLocalPosition;
            if (_bounceTarget != null)
                _bounceTarget.anchoredPosition = _bounceTargetBaseAnchoredPosition;

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                if (_linkedTargets[i] == null || i >= _linkedTargetBaseLocalPositions.Count)
                    continue;

                _linkedTargets[i].localPosition = _linkedTargetBaseLocalPositions[i];
            }
        }

        private float GetBounceDirection()
        {
            var mask = _targetRect.GetComponentInParent<RectMask2D>();
            if (mask == null)
                return 1f;

            var grid = _targetRect.GetComponentInParent<GridLayoutGroup>();
            if (IsInFirstGridRow(grid))
                return -1f;

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
    }
}
