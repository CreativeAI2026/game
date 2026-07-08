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
                _targetRect.localScale = Vector3.one;

            foreach (var linkedTarget in _linkedTargets)
            {
                if (linkedTarget != null)
                    linkedTarget.localScale = Vector3.one;
            }
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
    }
}
