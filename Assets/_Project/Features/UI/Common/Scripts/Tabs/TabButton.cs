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

        [SerializeField]
        private Image _selectedBackground;

        [SerializeField]
        private RectTransform _selectedUnderline;

        [SerializeField]
        private bool _showSelectedUnderline = true;

        [Header("Selection Animation")]
        [SerializeField]
        [Min(0f)]
        private float _selectionShowDuration = 0.22f;

        [SerializeField]
        [Min(0f)]
        private float _selectionHideDuration = 0.14f;

        [SerializeField]
        [Range(0.5f, 1f)]
        private float _selectionStartScale = 0.9f;

        private Button _button;
        public Button Button => _button ??= GetComponent<Button>();
        public Image Icon => _icon;
        private HoverScaleOnPointer _hoverScale;
        private bool _hasVisualHoverTarget;
        private bool _hasWarnedMissingVisualHoverTarget;
        private Image _selectedUnderlineImage;
        private Vector3 _selectionBackgroundScale = Vector3.one;
        private Vector3 _selectionUnderlineScale = Vector3.one;
        private bool _isSelected;

        private static readonly Color ActiveColor = new Color32(0x61, 0x67, 0x80, 0xff);
        private static readonly Color InactiveColor = new Color32(0xa9, 0xb2, 0xda, 0xff);

        private void Awake()
        {
            _button = GetComponent<Button>();
            _hoverScale = GetComponent<HoverScaleOnPointer>();
            if (_selectedBackground != null)
                _selectionBackgroundScale = _selectedBackground.rectTransform.localScale;
            if (_showSelectedUnderline && _selectedUnderline != null)
            {
                _selectedUnderlineImage = _selectedUnderline.GetComponent<Image>();
                _selectionUnderlineScale = _selectedUnderline.localScale;
            }
            InitializeSelectionVisuals();
            ConfigureHoverScaleTarget();
        }

        private void OnDisable()
        {
            _selectedBackground?.DOKill();
            _selectedBackground?.rectTransform.DOKill();
            _selectedUnderlineImage?.DOKill();
            _selectedUnderline?.DOKill();
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

        public void SetIconColor(Color color)
        {
            if (_icon == null)
                return;

            _icon.DOKill();
            _icon.color = color;
        }

        public void AlignUnderlineTo(RectTransform alignmentTarget)
        {
            if (_selectedUnderline == null || alignmentTarget == null)
                return;

            RectTransform parent = _selectedUnderline.parent as RectTransform;
            if (parent == null)
                return;

            var corners = new Vector3[4];
            alignmentTarget.GetWorldCorners(corners);
            Vector3 localBottom = parent.InverseTransformPoint((corners[0] + corners[3]) * 0.5f);
            Vector2 position = _selectedUnderline.anchoredPosition;
            position.y = localBottom.y;
            _selectedUnderline.anchoredPosition = position;
        }

        public void SetActive(
            bool isActive,
            float duration,
            bool dimInactive,
            bool allowSelectedBounce
        )
        {
            var targetColor = isActive || !dimInactive ? ActiveColor : InactiveColor;

            AnimateSelectionVisuals(isActive);

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

        private void AnimateSelectionVisuals(bool isActive)
        {
            if (_isSelected == isActive)
                return;

            _isSelected = isActive;
            KillSelectionTweens();

            if (isActive)
            {
                ShowSelectionVisuals();
                return;
            }

            HideSelectionVisuals();
        }

        private void InitializeSelectionVisuals()
        {
            if (_selectedBackground != null)
            {
                _selectedBackground.rectTransform.localScale = _selectionBackgroundScale;
                SetAlpha(_selectedBackground, 1f);
                _selectedBackground.gameObject.SetActive(false);
            }

            if (_selectedUnderline != null)
            {
                _selectedUnderline.localScale = _selectionUnderlineScale;
                SetAlpha(_selectedUnderlineImage, 1f);
                _selectedUnderline.gameObject.SetActive(false);
            }
        }

        private void ShowSelectionVisuals()
        {
            if (_selectedBackground != null)
            {
                _selectedBackground.gameObject.SetActive(true);
                SetAlpha(_selectedBackground, 0f);
                _selectedBackground.rectTransform.localScale =
                    _selectionBackgroundScale * _selectionStartScale;
                _selectedBackground.DOFade(1f, _selectionShowDuration).SetEase(Ease.OutQuad);
                _selectedBackground
                    .rectTransform.DOScale(_selectionBackgroundScale, _selectionShowDuration)
                    .SetEase(Ease.OutBack);
            }

            if (_showSelectedUnderline && _selectedUnderline != null)
            {
                _selectedUnderline.gameObject.SetActive(true);
                SetAlpha(_selectedUnderlineImage, 0f);
                _selectedUnderline.localScale = new Vector3(
                    0f,
                    _selectionUnderlineScale.y,
                    _selectionUnderlineScale.z
                );
                _selectedUnderlineImage?.DOFade(1f, _selectionShowDuration).SetEase(Ease.OutQuad);
                _selectedUnderline
                    .DOScaleX(_selectionUnderlineScale.x, _selectionShowDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        private void HideSelectionVisuals()
        {
            if (_selectedBackground != null && _selectedBackground.gameObject.activeSelf)
            {
                _selectedBackground
                    .DOFade(0f, _selectionHideDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        if (!_isSelected)
                            _selectedBackground.gameObject.SetActive(false);
                    });
            }

            if (_selectedUnderlineImage != null && _selectedUnderline.gameObject.activeSelf)
            {
                _selectedUnderlineImage
                    .DOFade(0f, _selectionHideDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        if (!_isSelected)
                            _selectedUnderline.gameObject.SetActive(false);
                    });
            }
        }

        private void KillSelectionTweens()
        {
            _selectedBackground?.DOKill();
            _selectedBackground?.rectTransform.DOKill();
            _selectedUnderlineImage?.DOKill();
            _selectedUnderline?.DOKill();
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
