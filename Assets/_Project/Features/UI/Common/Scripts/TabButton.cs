using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using TMPro;
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
        private TMP_Text _label;

        [SerializeField]
        private RectTransform _visualTarget;

        public Button Button { get; private set; }
        private HoverScaleOnPointer _hoverScale;
        private bool _hasVisualHoverTarget;

        private static readonly Color ActiveColor = Color.white;
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private void Awake()
        {
            Button = GetComponent<Button>();
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            CreateVisualTarget();
            ConfigureHoverScaleTarget();
        }

        private void CreateVisualTarget()
        {
            if (_icon == null || _icon.transform != transform)
                return;

            var rootImage = _icon;
            var visualObject = new GameObject(
                "Visual",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var visualRect = visualObject.GetComponent<RectTransform>();
            visualRect.SetParent(transform, false);
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;

            var visualImage = visualObject.GetComponent<Image>();
            visualImage.sprite = rootImage.sprite;
            visualImage.color = rootImage.color;
            visualImage.preserveAspect = rootImage.preserveAspect;
            visualImage.raycastTarget = false;

            rootImage.sprite = null;
            rootImage.color = Color.clear;
            _icon = visualImage;
            _visualTarget = visualRect;
        }

        private void ConfigureHoverScaleTarget()
        {
            if (_hoverScale == null)
                return;

            var target = _visualTarget != null ? _visualTarget : _icon?.rectTransform;
            if (target != null)
            {
                _hoverScale.SetTarget(target);
                _hasVisualHoverTarget = true;
                _hoverScale.SetBounceEnabled(true);
            }
            else
            {
                _hoverScale.SetTarget(null);
                _hasVisualHoverTarget = false;
                _hoverScale.SetBounceEnabled(false);
            }

            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        public void SetSelectionGroup(string group)
        {
            if (_hoverScale == null)
                return;

            _hoverScale.SetGroup(group);
            _hoverScale.SetReleaseLockOnOutsideClick(false);
        }

        public void Setup(Sprite icon, string label)
        {
            if (_icon != null)
                _icon.sprite = icon;
            if (_label != null)
            {
                _label.text = label;
                _label.gameObject.SetActive(!string.IsNullOrEmpty(label));
            }
        }

        public void SetActive(bool isActive, float duration) =>
            SetActive(isActive, duration, true, true);

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

            if (_label != null)
            {
                _label.DOKill();
                DOTween
                    .To(() => _label.color, x => _label.color = x, targetColor, duration)
                    .SetEase(Ease.OutQuad);
            }

            _hoverScale?.SetBounceEnabled(_hasVisualHoverTarget && allowSelectedBounce);

            if (isActive)
                _hoverScale?.AcquireLock();
            else if (_hoverScale != null && _hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }
    }
}
