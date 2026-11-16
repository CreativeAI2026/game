using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
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
            if (_iconView != null)
                _iconView.SetEmptyAlpha(EmptyIconAlpha);
            if (_countBadgeView != null)
            {
                RectTransform positionReference = null;
                if (_iconView != null)
                    positionReference =
                        _iconView.FitRect != null ? _iconView.FitRect : _iconView.IconRect;
                _countBadgeView.SetPositionReference(positionReference);
            }
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
            if (_iconView != null)
                _iconView.SetEmptyAlpha(EmptyIconAlpha);
            base.Refresh();
            if (_emptyView != null)
                _emptyView.SetEmpty(_item == null || _item.icon == null);
            ConfigureHover();
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            if (_emptyView != null)
                _emptyView.SetEmpty(true);
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

            InventoryManager inventory = InventoryManager.Instance;
            var stack =
                inventory != null ? inventory.GetAllItems().Find(s => s.Data == item) : null;
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
            if (_visualRootRect == null)
                _visualRootRect = transform.Find("VisualRoot") as RectTransform;
            if (_iconView == null)
                _iconView = GetComponentInChildren<SlotIconView>(true);
            if (_countBadgeView == null)
                _countBadgeView = GetComponentInChildren<SlotCountBadgeView>(true);
            if (_emptyView == null)
                _emptyView = GetComponentInChildren<SlotEmptyView>(true);
            if (_hoverView == null)
                _hoverView = GetComponentInChildren<SlotHoverView>(true);
            if (_frameView == null)
                _frameView = GetComponentInChildren<SlotFrameView>(true);
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
