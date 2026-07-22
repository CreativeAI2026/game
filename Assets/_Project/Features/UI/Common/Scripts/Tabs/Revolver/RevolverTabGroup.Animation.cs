using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    public sealed partial class RevolverTabGroup
    {
        private void AnimateSelection(int targetIndex)
        {
            KillSelectionTween();
            int step = _loop
                ? RevolverTabIndexUtility.ShortestStep(_selectedIndex, targetIndex, EntryCount)
                : targetIndex - _selectedIndex;
            float endPosition = _selectionPosition + step;
            int version = ++_animationVersion;

            _selectionTween = DOTween
                .To(
                    () => _selectionPosition,
                    value =>
                    {
                        _selectionPosition = value;
                        RefreshLayout();
                    },
                    endPosition,
                    _moveDuration
                )
                .SetEase(_ease)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    if (version != _animationVersion)
                        return;

                    _selectionTween = null;
                    CompleteSelection(targetIndex);
                })
                .OnKill(() =>
                {
                    if (version == _animationVersion)
                        _selectionTween = null;
                });

            RefreshLayout();
        }

        private void KillSelectionTween()
        {
            _animationVersion++;
            if (_selectionTween != null)
            {
                _selectionTween.Kill();
                _selectionTween = null;
            }
        }

        private void RefreshLayout()
        {
            if (!_built || _items.Count == 0)
                return;

            bool canInteract = _interactionEnabled && !IsAnimating;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null)
                    continue;

                float relativePosition = _loop
                    ? RevolverTabIndexUtility.SignedWrappedDistance(
                        item.DataIndex,
                        _selectionPosition,
                        EntryCount
                    )
                    : item.DataIndex - _selectionPosition;
                item.ApplyLayout(
                    RevolverTabLayoutCalculator.Calculate(relativePosition, _layout),
                    canInteract
                );
            }

            // Far items are placed first; the closest item is therefore rendered in front.
            for (int rank = 0; rank < _items.Count; rank++)
            {
                int itemIndex = FindItemAtRenderRank(rank);
                if (itemIndex >= 0 && _items[itemIndex] != null)
                    _items[itemIndex].transform.SetAsLastSibling();
            }
        }

        private int FindItemAtRenderRank(int targetRank)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null)
                    continue;

                float distance = GetAbsoluteDistance(_items[i]);
                int rank = 0;
                for (int j = 0; j < _items.Count; j++)
                {
                    if (i == j || _items[j] == null)
                        continue;

                    float otherDistance = GetAbsoluteDistance(_items[j]);
                    if (
                        otherDistance > distance
                        || (
                            Mathf.Approximately(otherDistance, distance)
                            && _items[j].DataIndex < _items[i].DataIndex
                        )
                    )
                        rank++;
                }

                if (rank == targetRank)
                    return i;
            }
            return -1;
        }

        private float GetAbsoluteDistance(RevolverTabItemView item) =>
            Mathf.Abs(
                _loop
                    ? RevolverTabIndexUtility.SignedWrappedDistance(
                        item.DataIndex,
                        _selectionPosition,
                        EntryCount
                    )
                    : item.DataIndex - _selectionPosition
            );

        private void ApplySelectedView()
        {
            if (_entries == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var view = _entries[i]?.View;
                if (view != null)
                    view.SetActive(i == _selectedIndex);
                if (i < _items.Count && _items[i] != null)
                    _items[i].SetSelected(i == _selectedIndex);
            }
        }
    }
}
