using System;
using System.Collections;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class MaterialSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float SelectedSlotScale = 1.08f;
        private const float IconPadding = 14f;
        private const float EmptyIconAlpha = 50f / 255f;

        private static readonly Color SelectedFrameColor = new Color(1f, 0.78f, 0.15f, 0.9f);
        private static readonly Color NormalFrameColor = new Color(1f, 1f, 1f, 0.2f);

        private TMP_Text _emptyText;
        private Image _frame;
        private RectTransform _visualRootRect;
        private Coroutine _materialAnimationRoutine;
        private bool _isSelected;
        private ItemStack _stack;

        public event Action<MaterialSlot> Clicked;
        public event Action<MaterialSlot> DoubleClicked;
        public ItemStack Stack => _stack;

        protected override void Awake()
        {
            base.Awake();
            _emptyText = transform.Find("EmptyText")?.GetComponent<TMP_Text>();
            _frame = GetComponent<Image>();
            ResolveVisualReferences();

            if (_hoverScale != null)
            {
                _hoverScale.SetGroup("craft-slots");
                _hoverScale.SetHoverScale(SelectedSlotScale);
                _hoverScale.SetBounceHeight(0f);
                _hoverScale.SetReleaseLockOnOutsideClick(false);
            }

            ApplyIconPadding();
            BindSlotHoverTarget();
            Clear();
            SetSelected(false);
        }

        public override void SetItem(ItemData item, int count = 1)
        {
            _stack = null;
            base.SetItem(item, count);
            ResolveVisualReferences();
            BindSlotHoverTarget();
        }

        public void SetMaterial(ItemData item, int count)
        {
            SetItem(item, count);
        }

        public void SetMaterial(ItemStack stack)
        {
            _stack = stack;
            base.SetItem(stack?.Data, stack == null ? 0 : 1);
            ResolveVisualReferences();
            BindSlotHoverTarget();
        }

        public void SetMaterialAnimated(ItemData item, int count)
        {
            SetItem(item, count);
            PlayMaterialChangedAnimation();
        }

        public void SetMaterialAnimated(ItemStack stack)
        {
            SetMaterial(stack);
            PlayMaterialChangedAnimation();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplySelectedVisual();

            if (selected)
                Select();
            else
                Deselect();
        }

        public void NormalizeVisualState()
        {
            ResolveVisualReferences();
            ApplyIconPadding();
            ApplyIconState();
            ApplySelectedVisual();
            BindSlotHoverTarget();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(_item == null || _item.icon == null);
        }

        private void ApplySelectedVisual()
        {
            if (_frame != null)
                _frame.color = _isSelected ? SelectedFrameColor : NormalFrameColor;
        }

        private Color GetCurrentFrameColor()
        {
            return _isSelected ? SelectedFrameColor : NormalFrameColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                DoubleClicked?.Invoke(this);
            else
                Clicked?.Invoke(this);
        }

        protected override void Refresh()
        {
            base.Refresh();
            ResolveVisualReferences();
            ApplyIconState();
            BindSlotHoverTarget();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(_item == null || _item.icon == null);
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            ResolveVisualReferences();
            ApplyIconState();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(true);
        }

        public void ClearMaterialAnimated(Action onCleared = null)
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialClearedRoutine(onCleared));
        }

        private void ApplyIconPadding()
        {
            if (_iconImage == null)
                return;

            var iconRect = _iconImage.rectTransform;

            if (_visualRootRect != null)
            {
                ApplyVisualRootPadding();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                return;
            }

            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.one * IconPadding;
            iconRect.offsetMax = -Vector2.one * IconPadding;
        }

        private void BindSlotHoverTarget()
        {
            if (_hoverScale == null)
                return;

            ResolveVisualReferences();

            RectTransform target =
                _visualRootRect != null ? _visualRootRect
                : _iconImage != null ? _iconImage.rectTransform
                : (RectTransform)transform;

            _hoverScale.SetTarget(target);
            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        private void ApplyIconState()
        {
            if (_iconImage == null)
                return;

            bool hasMaterial = _item != null && _item.icon != null;
            _iconImage.gameObject.SetActive(true);
            _iconImage.color = new Color(1f, 1f, 1f, hasMaterial ? 1f : EmptyIconAlpha);
        }

        private void PlayMaterialChangedAnimation()
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialChangedRoutine());
        }

        private void PlayMaterialClearedAnimation(Action onCleared = null)
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialClearedRoutine());
        }

        private IEnumerator PlayMaterialChangedRoutine()
        {
            const float duration = 0.24f;
            var animatedRect = GetAnimatedVisualRect();
            if (animatedRect != null)
                animatedRect.localScale = Vector3.one * 0.55f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(t));

                if (animatedRect != null)
                    animatedRect.localScale = Vector3.one * scale;
                if (_frame != null)
                    _frame.color = Color.Lerp(Color.white, GetCurrentFrameColor(), t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (animatedRect != null)
                animatedRect.localScale = Vector3.one;
            ApplySelectedVisual();

            _materialAnimationRoutine = null;
        }

        private IEnumerator PlayMaterialClearedRoutine(Action onCleared = null)
        {
            const float duration = 0.16f;
            float elapsed = 0f;
            var animatedRect = GetAnimatedVisualRect();

            Vector3 startScale = animatedRect != null ? animatedRect.localScale : Vector3.one;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                if (animatedRect != null)
                {
                    animatedRect.localScale = Vector3.Lerp(startScale, Vector3.one * 0.35f, t);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Clear();

            onCleared?.Invoke();

            _materialAnimationRoutine = null;
            PlayMaterialChangedAnimation();
        }

        private void StopMaterialAnimation()
        {
            if (_materialAnimationRoutine == null)
                return;

            StopCoroutine(_materialAnimationRoutine);
            _materialAnimationRoutine = null;

            var animatedRect = GetAnimatedVisualRect();
            if (animatedRect != null)
                animatedRect.localScale = Vector3.one;
        }

        private RectTransform GetAnimatedVisualRect()
        {
            ResolveVisualReferences();
            return _visualRootRect != null ? _visualRootRect : _iconImage?.rectTransform;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void ResolveVisualReferences()
        {
            if (_visualRootRect == null)
                _visualRootRect = FindChildRectIgnoreCase("VisualRoot");
            if (_visualRootRect == null)
                _visualRootRect = CreateVisualRoot();

            if (
                _iconImage != null
                && _visualRootRect != null
                && _iconImage.rectTransform.parent != _visualRootRect
            )
            {
                _iconImage.rectTransform.SetParent(_visualRootRect, false);
                _iconImage.rectTransform.SetAsFirstSibling();
            }
        }

        private RectTransform FindChildRectIgnoreCase(string childName)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (string.Equals(rect.name, childName, StringComparison.OrdinalIgnoreCase))
                    return rect;
            }

            return null;
        }

        private RectTransform CreateVisualRoot()
        {
            var visualRootObject = new GameObject("VisualRoot", typeof(RectTransform));
            var rect = visualRootObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            ApplyVisualRootRect(rect);
            return rect;
        }

        private void ApplyVisualRootPadding()
        {
            if (_visualRootRect != null)
                ApplyVisualRootRect(_visualRootRect);
        }

        private static void ApplyVisualRootRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * IconPadding;
            rect.offsetMax = -Vector2.one * IconPadding;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }
    }
}
