using System;
using System.Collections;
using System.Collections.Generic;
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
        private const float EmptyIconAlpha = 50f / 255f;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotEmptyView _emptyView;

        [SerializeField]
        private SlotHoverView _hoverView;

        [SerializeField]
        private SlotFrameView _frameView;

        [SerializeField]
        private TMP_Text _slotLabel;

        private Coroutine _materialAnimationRoutine;
        private bool _isSelected;
        private ItemStack _stack;
        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotHoverView HoverView => _hoverView;

        public event Action<MaterialSlot> Clicked;
        public event Action<MaterialSlot> DoubleClicked;
        public ItemStack Stack => _stack;

        protected override void Awake()
        {
            ResolveViewReferences();
            _iconView?.SetEmptyAlpha(EmptyIconAlpha);
            base.Awake();
            ConfigureHover();
            Clear();
            SetSelected(false);
        }

        public override void SetItem(ItemData item, int count = 1)
        {
            _stack = null;
            base.SetItem(item, count);
            ConfigureHover();
        }

        public void SetMaterial(ItemData item, int count)
        {
            SetItem(item, count);
        }

        public void SetMaterial(ItemStack stack)
        {
            _stack = stack;
            base.SetItem(stack?.Data, stack == null ? 0 : 1);
            ConfigureHover();
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
            _frameView?.SetSelected(selected);

            if (selected)
                Select();
            else
                Deselect();
        }

        public void NormalizeVisualState()
        {
            ResolveViewReferences();
            Refresh();
            _frameView?.SetSelected(_isSelected);
            ConfigureHover();
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
            ResolveViewReferences();
            _iconView?.SetEmptyAlpha(EmptyIconAlpha);
            base.Refresh();
            _emptyView?.SetEmpty(_item == null);
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            _emptyView?.SetEmpty(true);
        }

        public void ClearMaterialAnimated(Action onCleared = null)
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialClearedRoutine(onCleared));
        }

        private void ConfigureHover()
        {
            ResolveViewReferences();
            if (_hoverView == null)
                return;

            _hoverView.Bind(_visualRootRect);
            _hoverView.SetGroup("craft-slots");
            _hoverView.SetHoverScale(SelectedSlotScale);
            _hoverView.SetBounceHeight(0f);
            _hoverView.SetReleaseLockOnOutsideClick(false);
        }

        private void PlayMaterialChangedAnimation()
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialChangedRoutine());
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
                if (_frameView != null)
                    _frameView.SetColor(
                        Color.Lerp(Color.white, _frameView.GetColor(_isSelected), t)
                    );

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (animatedRect != null)
                animatedRect.localScale = Vector3.one;
            _frameView?.SetSelected(_isSelected);
            _materialAnimationRoutine = null;
        }

        private IEnumerator PlayMaterialClearedRoutine(Action onCleared)
        {
            const float duration = 0.16f;
            float elapsed = 0f;
            var animatedRect = GetAnimatedVisualRect();
            Vector3 startScale = animatedRect != null ? animatedRect.localScale : Vector3.one;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                if (animatedRect != null)
                    animatedRect.localScale = Vector3.Lerp(startScale, Vector3.one * 0.35f, t);

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
            ResolveViewReferences();
            return _visualRootRect != transform ? _visualRootRect : null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void ResolveViewReferences()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            _iconView ??= GetComponentInChildren<SlotIconView>(true);
            _emptyView ??= GetComponentInChildren<SlotEmptyView>(true);
            _hoverView ??= GetComponentInChildren<SlotHoverView>(true);
            _frameView ??= GetComponentInChildren<SlotFrameView>(true);
            _slotLabel ??= transform.Find("SlotLabel")?.GetComponent<TMP_Text>();

            WarnIfMissing(_visualRootRect, "VisualRoot");
            WarnIfMissing(_iconView, nameof(SlotIconView));
            WarnIfMissing(_emptyView, nameof(SlotEmptyView));
            WarnIfMissing(_hoverView, nameof(SlotHoverView));
            WarnIfMissing(_frameView, nameof(SlotFrameView));
            WarnIfMissing(_slotLabel, "SlotLabel");
        }

        private void WarnIfMissing(UnityEngine.Object reference, string referenceName)
        {
            if (reference != null || !_warnedMissingViews.Add(referenceName))
                return;

            Debug.LogWarning(
                $"{nameof(MaterialSlot)} '{name}' に {referenceName} がないため、該当表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
