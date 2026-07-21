using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot : BaseItemSlot, IPointerClickHandler
    {
        private ItemStack _itemStack;
        private InventoryView _controller;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotCountBadgeView _countBadgeView;

        [SerializeField]
        private SlotHoverView _hoverView;

        [SerializeField]
        private SlotSelectionView _selectionView;

        [SerializeField]
        private SlotMarkerView _markerView;

        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotCountBadgeView CountBadgeView => _countBadgeView;
        protected override SlotHoverView HoverView => _hoverView;

        protected override void Awake()
        {
            ResolveViewReferences();
            base.Awake();
            _controller = GetComponentInParent<InventoryView>();
            ConfigureVisualRootHover();
            RefreshSelectionVisuals();
        }

        private void OnEnable()
        {
            ResolveViewReferences();
            ConfigureVisualRootHover();
            RefreshSelectionVisuals();
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
            SetEquipped(stack?.IsEquipped ?? false);
        }

        public ItemStack Stack => _itemStack;

        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            ResolveViewReferences();
            _hoverView?.SetReleaseLockOnOutsideClick(release);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_controller != null)
            {
                if (
                    eventData.button == PointerEventData.InputButton.Left
                    && eventData.clickCount >= 2
                )
                {
                    _controller.SelectSlotByDoubleClick(this);
                    return;
                }

                _controller.SelectSlotByClick(this);
                return;
            }

            Select();
        }

        private void ConfigureVisualRootHover()
        {
            ResolveViewReferences();
            _hoverView?.Bind();
        }

        private void ResolveViewReferences()
        {
            WarnIfMissing(_visualRootRect, "VisualRoot");
            WarnIfMissing(_iconView, nameof(SlotIconView));
            WarnIfMissing(_countBadgeView, nameof(SlotCountBadgeView));
            WarnIfMissing(_hoverView, nameof(SlotHoverView));
            WarnIfMissing(_selectionView, nameof(SlotSelectionView));
            WarnIfMissing(_markerView, nameof(SlotMarkerView));
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            _iconView ??= GetComponentInChildren<SlotIconView>(true);
            _countBadgeView ??= GetComponentInChildren<SlotCountBadgeView>(true);
            _hoverView ??= GetComponentInChildren<SlotHoverView>(true);
            _selectionView ??= GetComponentInChildren<SlotSelectionView>(true);
            _markerView ??= GetComponentInChildren<SlotMarkerView>(true);
        }
#endif

        private void WarnIfMissing(Object reference, string referenceName)
        {
            if (reference != null || !_warnedMissingViews.Add(referenceName))
                return;

            Debug.LogWarning(
                $"{nameof(ItemSlot)} '{name}' に {referenceName} がないため、該当表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
