using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class EquipmentSlot : BaseItemSlot, IPointerDownHandler, IPointerClickHandler
    {
        private const float SelectedIconScale = 1.08f;
        private const float EmptyIconAlpha = 0f;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotCountBadgeView _countBadgeView;

        [SerializeField]
        private SlotEmptyView _emptyView;

        [SerializeField]
        private SlotHoverView _hoverView;

        [SerializeField]
        private SlotFrameView _frameView;

        private bool _inputLocked;
        private bool _acceptPointerClick;
        private ItemStack _stack;
        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotCountBadgeView CountBadgeView => _countBadgeView;
        protected override SlotHoverView HoverView => _hoverView;
        protected override SlotFrameView FrameView => _frameView;

        public Button Button { get; private set; }
        public event Action<EquipmentSlot> Clicked;
        public event Action<EquipmentSlot> DoubleClicked;

        public void ApplyCustomFrameOrientation(bool mirrored, float duration = 0f)
        {
            ResolveViewReferences(false);
            RectTransform frameRect = _frameView?.FrameRect;
            if (frameRect == null || _frameView.Role != SlotFrameRole.Custom)
                return;

            Vector3 frameScale = frameRect.localScale;
            float frameScaleMagnitude = Mathf.Abs(frameScale.x);
            float targetScaleX = mirrored ? -frameScaleMagnitude : frameScaleMagnitude;
            bool orientationChanged = (frameScale.x < 0f) != mirrored;

            Vector2 contentPosition = Vector2.zero;
            if (mirrored)
                contentPosition.x = frameRect.anchoredPosition.x * 2f;

            RectTransform iconArea = _iconView?.FitRect;
            RectTransform iconRect = _iconView?.IconRect;
            RectTransform emptyRect = _emptyView?.EmptyRect;

            frameRect.DOKill();
            iconArea?.DOKill();
            iconRect?.DOKill();
            emptyRect?.DOKill();

            if (duration > 0f && orientationChanged && Application.isPlaying)
            {
                frameRect.DOScaleX(targetScaleX, duration).SetEase(Ease.InOutSine);
                iconArea?.DOAnchorPos(contentPosition, duration).SetEase(Ease.InOutSine);
                iconRect
                    ?.DOAnchorPos(contentPosition, duration)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(() => _iconView?.RefreshLayout());
                emptyRect?.DOAnchorPos(contentPosition, duration).SetEase(Ease.InOutSine);
            }
            else
            {
                frameScale.x = targetScaleX;
                frameRect.localScale = frameScale;
                if (iconArea != null)
                    iconArea.anchoredPosition = contentPosition;
                if (emptyRect != null)
                    emptyRect.anchoredPosition = contentPosition;
            }

            _iconView?.RefreshLayout();
        }

        public new ItemData Item
        {
            get => _stack?.Data;
            set => SetItem(value, FindInventoryCount(value));
        }

        public ItemStack Stack => _stack;

        public void Init()
        {
            ResolveViewReferences();
            Button = GetComponent<Button>();
            _iconView?.SetEmptyAlpha(EmptyIconAlpha);
            ConfigureHover();
            Refresh();
        }

        public override void SetItem(ItemData item, int count = 1)
        {
            _stack = null;
            base.SetItem(item, count);
        }

        public void SetStack(ItemStack stack)
        {
            _stack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
        }

        protected override void Refresh()
        {
            ResolveViewReferences();
            _iconView?.SetEmptyAlpha(EmptyIconAlpha);
            base.Refresh();
            _emptyView?.SetEmpty(_item == null || _item.icon == null);
            ConfigureHover();
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            _emptyView?.SetEmpty(true);
        }

        public new void ClearAnimated(Action onComplete = null)
        {
            _stack = null;
            base.ClearAnimated(onComplete);
        }

        public void UpdateCount()
        {
            SetCount(_stack?.Count ?? FindInventoryCount(_item));
        }

        public void EquipAnimated(ItemData item)
        {
            SetItemAnimated(item, FindInventoryCount(item));
        }

        public void EquipAnimated(ItemStack stack)
        {
            SetItemAnimated(stack?.Data, stack?.Count ?? 0);
            _stack = stack;
        }

        public void SetSelected(bool selected)
        {
            ResolveViewReferences();
            if (selected)
                Select();
            else
                Deselect();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _acceptPointerClick =
                !_inputLocked && eventData.button == PointerEventData.InputButton.Left;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bool accepted = _acceptPointerClick;
            _acceptPointerClick = false;

            if (!accepted || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                DoubleClicked?.Invoke(this);
            else
                Clicked?.Invoke(this);
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            if (Button != null)
                Button.interactable = !locked;
        }

        private static int FindInventoryCount(ItemData item)
        {
            if (item == null)
                return 0;

            var stack = InventoryManager.Instance?.GetAllItems().Find(s => s.Data == item);
            return stack?.Count ?? 1;
        }

        private void ConfigureHover()
        {
            ResolveViewReferences(false);
            if (_hoverView == null)
                return;

            _hoverView.Bind();
            _hoverView.SetGroup("equipment-slots");
            _hoverView.SetHoverScale(SelectedIconScale);
            _hoverView.SetBounceHeight(0f);
            _hoverView.SetReleaseLockOnOutsideClick(false);
        }

        private void ResolveViewReferences(bool warn = true)
        {
            if (!warn)
                return;

            WarnIfMissing(_visualRootRect, "VisualRoot");
            WarnIfMissing(_iconView, nameof(SlotIconView));
            WarnIfMissing(_countBadgeView, nameof(SlotCountBadgeView));
            WarnIfMissing(_emptyView, nameof(SlotEmptyView));
            WarnIfMissing(_hoverView, nameof(SlotHoverView));
            WarnIfMissing(_frameView, nameof(SlotFrameView));
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            _iconView ??= GetComponentInChildren<SlotIconView>(true);
            _countBadgeView ??= GetComponentInChildren<SlotCountBadgeView>(true);
            _emptyView ??= GetComponentInChildren<SlotEmptyView>(true);
            _hoverView ??= GetComponentInChildren<SlotHoverView>(true);
            _frameView ??= GetComponentInChildren<SlotFrameView>(true);
        }
#endif

        private void WarnIfMissing(UnityEngine.Object reference, string referenceName)
        {
            if (reference != null || !_warnedMissingViews.Add(referenceName))
                return;

            Debug.LogWarning(
                $"{nameof(EquipmentSlot)} '{name}' に {referenceName} がないため、該当表示をスキップします。PrefabまたはScene上で設定してください。",
                this
            );
        }
    }
}
