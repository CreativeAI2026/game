using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public enum ItemSlotDragOrigin
    {
        Inventory,
        Quick,
        Equipment,
    }

    public readonly struct ItemSlotDragPayload
    {
        public ItemSlotDragPayload(ItemStack stack, ItemSlotDragOrigin origin, int slotIndex)
        {
            Stack = stack;
            Origin = origin;
            SlotIndex = slotIndex;
        }

        public ItemStack Stack { get; }
        public ItemSlotDragOrigin Origin { get; }
        public int SlotIndex { get; }
    }

    public static class ItemSlotDragContext
    {
        private static ItemSlotDragPayload _payload;
        private static RectTransform _ghostRect;
        private static Canvas _ghostCanvas;

        public static bool IsDragging { get; private set; }
        public static event Action<ItemSlotDragPayload> Started;
        public static event Action Ended;

        public static bool Begin(
            ItemStack stack,
            ItemSlotDragOrigin origin,
            Component source,
            PointerEventData eventData,
            int slotIndex = -1
        )
        {
            if (stack?.Data == null || source == null || eventData == null)
                return false;

            Clear();

            _payload = new ItemSlotDragPayload(stack, origin, slotIndex);
            IsDragging = true;
            CreateGhost(source, stack.Data.icon);
            Move(eventData);
            Started?.Invoke(_payload);
            return true;
        }

        public static bool TryGet(out ItemSlotDragPayload payload)
        {
            payload = _payload;
            return IsDragging && payload.Stack?.Data != null;
        }

        public static void Move(PointerEventData eventData)
        {
            if (eventData == null)
                return;

            MoveToScreenPosition(eventData.position);
        }

        public static void MoveToScreenPosition(Vector2 screenPosition)
        {
            if (!IsDragging || _ghostRect == null || _ghostCanvas == null)
                return;

            Camera camera =
                _ghostCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : _ghostCanvas.worldCamera;
            if (
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_ghostCanvas.transform,
                    screenPosition,
                    camera,
                    out Vector2 localPosition
                )
            )
                _ghostRect.anchoredPosition = localPosition;
        }

        public static void Clear()
        {
            bool wasDragging = IsDragging;
            _payload = default;
            IsDragging = false;

            if (_ghostRect != null)
                UnityEngine.Object.Destroy(_ghostRect.gameObject);
            _ghostRect = null;
            _ghostCanvas = null;

            if (wasDragging)
                Ended?.Invoke();
        }

        private static void CreateGhost(Component source, Sprite sprite)
        {
            Canvas canvas = source.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || sprite == null)
                return;

            var ghost = new GameObject(
                "ItemDragGhost",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup)
            );
            ghost.transform.SetParent(canvas.transform, false);
            ghost.transform.SetAsLastSibling();

            _ghostCanvas = canvas;
            _ghostRect = ghost.GetComponent<RectTransform>();
            _ghostRect.anchorMin = _ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostRect.pivot = new Vector2(0.5f, 0.5f);

            float size = 72f;
            if (source.transform is RectTransform sourceRect)
                size = Mathf.Clamp(
                    Mathf.Min(sourceRect.rect.width, sourceRect.rect.height) * 0.95f,
                    76f,
                    112f
                );
            _ghostRect.sizeDelta = new Vector2(size, size);

            Image image = ghost.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CanvasGroup group = ghost.GetComponent<CanvasGroup>();
            group.alpha = 0.7f;
            group.blocksRaycasts = false;
            group.interactable = false;
            ghost.AddComponent<ItemDragGhostFollower>();
        }
    }

    internal sealed class ItemDragGhostFollower : MonoBehaviour
    {
        private void LateUpdate()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed)
            {
                ItemSlotDragContext.Clear();
                return;
            }

            ItemSlotDragContext.MoveToScreenPosition(pointer.position.ReadValue());
        }
    }

    public partial class ItemSlot
        : BaseItemSlot,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        private ItemStack _itemStack;
        private InventoryView _controller;
        private bool _showCount = true;
        private CanvasGroup _dragCanvasGroup;
        private float _dragOriginalAlpha = 1f;
        private bool _dragging;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotCountBadgeView _countBadgeView;

        [SerializeField]
        private SlotFrameView _frameView;

        [SerializeField]
        private SlotHoverView _hoverView;

        [SerializeField]
        private SlotMarkerView _markerView;

        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotCountBadgeView CountBadgeView => _showCount ? _countBadgeView : null;
        protected override SlotHoverView HoverView => _hoverView;
        protected override SlotFrameView FrameView => _frameView;

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
            ItemSlotDragContext.Ended += OnDragEnded;
            ResolveViewReferences();
            ConfigureVisualRootHover();
            RefreshSelectionVisuals();
        }

        private void OnDisable()
        {
            FinishDragging();
            ItemSlotDragContext.Ended -= OnDragEnded;
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
            SetEquipped(stack?.IsEquipped ?? false);
        }

        public ItemStack Stack => _itemStack;

        public void SetShowCount(bool show)
        {
            _showCount = show;
            if (show)
                _countBadgeView?.SetCount(_item, _count);
            else
                _countBadgeView?.Hide();
        }

        public void RefreshCountLayout() => _countBadgeView?.RefreshLayout();

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _itemStack?.Data == null)
                return;

            if (
                !ItemSlotDragContext.Begin(
                    _itemStack,
                    ItemSlotDragOrigin.Inventory,
                    this,
                    eventData
                )
            )
                return;

            _controller?.ClearSelection();
            if (_dragCanvasGroup == null)
                _dragCanvasGroup = GetComponent<CanvasGroup>();
            if (_dragCanvasGroup == null)
                _dragCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            _dragOriginalAlpha = _dragCanvasGroup.alpha;
            _dragCanvasGroup.alpha = 0.45f;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
                ItemSlotDragContext.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => FinishDragging();

        private void FinishDragging()
        {
            if (!_dragging)
                return;

            if (_dragCanvasGroup != null)
                _dragCanvasGroup.alpha = _dragOriginalAlpha;
            _dragging = false;
            ItemSlotDragContext.Clear();
        }

        private void OnDragEnded()
        {
            if (!_dragging)
                return;

            if (_dragCanvasGroup != null)
                _dragCanvasGroup.alpha = _dragOriginalAlpha;
            _dragging = false;
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
            WarnIfMissing(_frameView, nameof(SlotFrameView));
            WarnIfMissing(_hoverView, nameof(SlotHoverView));
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
            _frameView ??= GetComponentInChildren<SlotFrameView>(true);
            _hoverView ??= GetComponentInChildren<SlotHoverView>(true);
            _markerView ??= GetComponentInChildren<SlotMarkerView>(true);
        }
#endif

        private void WarnIfMissing(UnityEngine.Object reference, string referenceName)
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
