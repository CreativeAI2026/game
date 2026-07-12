using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class TabButton : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _label;

        public Button Button { get; private set; }
        private HoverScaleOnPointer _hoverScale;

        private static readonly Color ActiveColor = Color.white;
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private void Awake()
        {
            Button = GetComponent<Button>();
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            CreateVisualTarget();
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

            _hoverScale?.SetTarget(visualRect);
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

        public void SetActive(bool isActive, float duration)
        {
            var targetColor = isActive ? ActiveColor : InactiveColor;

            if (_icon != null)
                DOTween
                    .To(() => _icon.color, x => _icon.color = x, targetColor, duration)
                    .SetEase(Ease.OutQuad);

            if (isActive)
                _hoverScale?.AcquireLock();
            else if (_hoverScale != null && _hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }
    }
}
