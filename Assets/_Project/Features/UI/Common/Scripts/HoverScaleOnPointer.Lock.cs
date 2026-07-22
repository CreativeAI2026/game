using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    public partial class HoverScaleOnPointer
    {
        private static readonly Dictionary<string, HoverScaleOnPointer> _lockedInstances = new();

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

        private void OnDisable()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            _currentTween?.Kill();
            _currentTween = null;
            StopBounce();

            if (_targetRect != null)
                _targetRect.localScale = _baseLocalScale;

            for (int i = 0; i < _linkedTargets.Count; i++)
            {
                var linkedTarget = _linkedTargets[i];
                if (linkedTarget != null)
                {
                    linkedTarget.localScale =
                        i < _linkedTargetBaseLocalScales.Count
                            ? _linkedTargetBaseLocalScales[i]
                            : linkedTarget.localScale;
                }
            }
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
    }
}
