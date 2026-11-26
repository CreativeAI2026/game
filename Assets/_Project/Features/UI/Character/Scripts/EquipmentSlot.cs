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
        private HoverScaleOnPointer _hover;

        [SerializeField]
        private SlotFrameView _frameView;

        private bool _acceptPointerClick;
        private ItemStack _stack;
        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotCountBadgeView CountBadgeView => _countBadgeView;
        protected override HoverScaleOnPointer Hover => _hover;
        protected override SlotFrameView FrameView => _frameView;

        public event Action<EquipmentSlot> Clicked;

        public ItemStack Stack => _stack;

        public void Init()
        {
            ResolveViewReferences();
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
        }

        public override void Clear()
        {
            _stack = null;
            base.Clear();
            if (_emptyView != null)
                _emptyView.SetEmpty(true);
        }

        public void UpdateCount()
        {
            SetCount(_stack?.Count ?? FindInventoryCount(_item));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _acceptPointerClick = eventData.button == PointerEventData.InputButton.Left;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            bool accepted = _acceptPointerClick;
            _acceptPointerClick = false;

            if (!accepted || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                return;

            Clicked?.Invoke(this);
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

        private void ResolveViewReferences(bool warn = true)
        {
            if (!warn)
                return;

            WarnIfMissing(_visualRootRect, "VisualRoot");
            WarnIfMissing(_iconView, nameof(SlotIconView));
            WarnIfMissing(_countBadgeView, nameof(SlotCountBadgeView));
            WarnIfMissing(_emptyView, nameof(SlotEmptyView));
            WarnIfMissing(_hover, nameof(HoverScaleOnPointer));
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
            if (_hover == null)
                _hover = GetComponentInChildren<HoverScaleOnPointer>(true);
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
