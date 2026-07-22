using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button))]
    public class TabButton : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private RectTransform _visualTarget;

        private Button _button;
        public Button Button => _button ??= GetComponent<Button>();
        public Image Icon => _icon;
        private HoverScaleOnPointer _hoverScale;
        private bool _hasVisualHoverTarget;
        private bool _hasWarnedMissingVisualHoverTarget;

        private static readonly Color ActiveColor = Color.white;
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private void Awake()
        {
            _button = GetComponent<Button>();
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            ConfigureHoverScaleTarget();
        }

        private void ConfigureHoverScaleTarget()
        {
            if (_hoverScale == null)
                return;

            var target = ResolveVisualHoverTarget();
            if (target != null)
            {
                _hoverScale.SetTarget(target);
                _hasVisualHoverTarget = true;
                _hoverScale.SetBounceAllowed(true);
            }
            else
            {
                _hasVisualHoverTarget = false;
                _hoverScale.SetBounceAllowed(false);
                _hoverScale.enabled = false;
                WarnMissingVisualHoverTargetOnce();
                return;
            }

            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        private RectTransform ResolveVisualHoverTarget()
        {
            if (_visualTarget != null && _visualTarget != transform)
                return _visualTarget;

            if (_icon != null && _icon.rectTransform != transform)
                return _icon.rectTransform;

            return null;
        }

        private void WarnMissingVisualHoverTargetOnce()
        {
            if (_hasWarnedMissingVisualHoverTarget)
                return;

            _hasWarnedMissingVisualHoverTarget = true;
            Debug.LogWarning(
                $"{nameof(TabButton)} '{name}' にRoot以外のVisualTargetまたはIconが設定されていないため、Hover演出を無効化しました。Prefab上で参照を設定してください。",
                this
            );
        }

        public void SetSelectionGroup(string group)
        {
            if (_hoverScale == null)
                return;

            _hoverScale.SetGroup(group);
            _hoverScale.SetReleaseLockOnOutsideClick(false);
        }

        public void ConfigureHoverEffects(bool enableHoverScale, bool enableBounce)
        {
            if (_hoverScale == null)
                return;

            _hoverScale.SetHoverScaleEnabled(enableHoverScale);
            _hoverScale.SetBounceEnabled(enableBounce);
        }

        private void SetIcon(Sprite icon)
        {
            if (_icon != null)
                _icon.sprite = icon;
        }

        public void Bind(TabDefinition definition)
        {
            SetIcon(definition != null ? definition.Icon : null);
        }

        public void SetActive(
            bool isActive,
            float duration,
            bool dimInactive,
            bool allowSelectedBounce
        )
        {
            var targetColor = isActive || !dimInactive ? ActiveColor : InactiveColor;

            if (_icon != null)
            {
                _icon.DOKill();
                DOTween
                    .To(() => _icon.color, x => _icon.color = x, targetColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            _hoverScale?.SetBounceAllowed(_hasVisualHoverTarget && allowSelectedBounce);

            if (_hoverScale == null || !_hasVisualHoverTarget)
                return;

            if (isActive)
                _hoverScale.AcquireLock();
            else if (_hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }
    }
}
