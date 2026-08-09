using CreativeAI.UI.InventoryUI;
using UnityEngine;

namespace CreativeAI.UI
{
    public class SlotHoverView : MonoBehaviour
    {
        [SerializeField]
        private HoverScaleOnPointer _hoverScale;

        [SerializeField]
        private RectTransform _animationTarget;

        [SerializeField, Min(0.1f)]
        private float _restScale = 1.08f;

        private bool _hasWarnedMissingHoverScale;
        private bool _hasWarnedMissingVisualRoot;

        public void Bind()
        {
            if (_hoverScale == null)
            {
                WarnMissingHoverScaleOnce();
                return;
            }

            if (_animationTarget == null)
            {
                WarnMissingVisualRootOnce();
                _hoverScale.SetLockEnabled(false);
                _hoverScale.SetBounceEnabled(false);
                _hoverScale.enabled = false;
                return;
            }

            _animationTarget.localScale = Vector3.one * _restScale;
            _hoverScale.SetTarget(_animationTarget);
            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        public void SetGroup(string group)
        {
            ResolveHoverScale()?.SetGroup(group);
        }

        public void SetHoverScale(float scale)
        {
            ResolveHoverScale()?.SetHoverScale(scale);
        }

        public void SetBounceHeight(float height)
        {
            ResolveHoverScale()?.SetBounceHeight(height);
        }

        public void SetBounceEnabled(bool enabled)
        {
            ResolveHoverScale()?.SetBounceEnabled(enabled);
        }

        public void SetReleaseLockOnOutsideClick(bool release)
        {
            ResolveHoverScale()?.SetReleaseLockOnOutsideClick(release);
        }

        public void AcquireLock()
        {
            ResolveHoverScale()?.AcquireLock();
        }

        public void ReleaseLock()
        {
            var hoverScale = ResolveHoverScale();
            if (hoverScale != null && hoverScale.IsLocked())
                hoverScale.ReleaseLock();
        }

        private HoverScaleOnPointer ResolveHoverScale()
        {
            if (_hoverScale == null)
                WarnMissingHoverScaleOnce();
            return _hoverScale;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _hoverScale ??= GetComponent<HoverScaleOnPointer>();
            _animationTarget ??= GetComponent<RectTransform>();
        }
#endif

        private void WarnMissingHoverScaleOnce()
        {
            if (_hasWarnedMissingHoverScale)
                return;

            _hasWarnedMissingHoverScale = true;
            Debug.LogWarning(
                $"{nameof(SlotHoverView)} '{name}' に {nameof(HoverScaleOnPointer)} がないため、Hover設定をスキップします。Prefab上で設定してください。",
                this
            );
        }

        private void WarnMissingVisualRootOnce()
        {
            if (_hasWarnedMissingVisualRoot)
                return;

            _hasWarnedMissingVisualRoot = true;
            Debug.LogWarning(
                $"{nameof(SlotHoverView)} '{name}' にVisualRootがないため、Hover設定をスキップします。Slot RootはHover対象にしません。",
                this
            );
        }
    }
}
