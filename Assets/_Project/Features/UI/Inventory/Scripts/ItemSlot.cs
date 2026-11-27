using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
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
            if (eventData != null)
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
            var parentCanvas = source.GetComponentInParent<Canvas>();
            Canvas canvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
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

    public class ItemSlot
        : BaseItemSlot,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
    {
        private ItemStack _itemStack;
        private ItemListView _controller;
        private bool _showCount = true;
        private CanvasGroup _dragCanvasGroup;
        private float _dragOriginalAlpha = 1f;
        private bool _dragging;

        [Header("印(調合の完成品スロットのように、印を持たない置き方もある)")]
        [SerializeField]
        private GameObject _equippedMarker;

        [SerializeField]
        private GameObject _craftAssignedMarker;

        [SerializeField]
        private GameObject _equippedDimOverlay;

        private bool _isEquipped;
        private bool _isCraftAssigned;

        protected override bool ShowsCount => _showCount;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponentInParent<ItemListView>();
            RefreshSelectionVisuals();
        }

        private void OnEnable()
        {
            ItemSlotDragContext.Ended += OnDragEnded;
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
            var equipment = InventoryManager.CurrentEquipment;
            SetEquipped(stack != null && equipment != null && equipment.Contains(stack));
        }

        public ItemStack Stack => _itemStack;

        public void SetShowCount(bool show)
        {
            _showCount = show;
            if (CountBadge == null)
                return;
            if (show)
                CountBadge.SetCount(_item, _count);
            else
                CountBadge.Hide();
        }

        public void RefreshCountLayout()
        {
            if (CountBadge != null)
                CountBadge.RefreshLayout();
        }

        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            if (Hover != null)
                Hover.SetReleaseLockOnOutsideClick(release);
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

            if (_controller != null)
                _controller.ClearSelection();
            if (_dragCanvasGroup == null)
                TryGetComponent(out _dragCanvasGroup);
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

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            AutoAssignSlotViews();
            if (_equippedMarker == null)
                _equippedMarker = FindChild("VisualRoot/EquippedMarker");
            if (_craftAssignedMarker == null)
                _craftAssignedMarker = FindChild("VisualRoot/CraftAssignedMarker");
            if (_equippedDimOverlay == null)
                _equippedDimOverlay = FindChild("VisualRoot/EquippedDimOverlay");
        }

        private GameObject FindChild(string path)
        {
            var child = transform.Find(path);
            return child != null ? child.gameObject : null;
        }
#endif

        protected override void RefreshSelectionVisuals()
        {
            if (FrameView != null)
                FrameView.SetSelected(_isSlotSelected);
        }

        public void SetEquipped(bool isEquipped)
        {
            _isEquipped = isEquipped;
            ApplyMarkers();
        }

        public void SetCraftAssigned(bool isAssigned)
        {
            _isCraftAssigned = isAssigned;
            ApplyMarkers();
        }

        // 装備中は印と暗幕を出す。調合に割り当てた印は、装備中でないときだけ出す
        private void ApplyMarkers()
        {
            if (_equippedMarker != null)
                _equippedMarker.SetActive(_isEquipped);
            if (_equippedDimOverlay != null)
                _equippedDimOverlay.SetActive(_isEquipped);
            if (_craftAssignedMarker != null)
                _craftAssignedMarker.SetActive(_isCraftAssigned && !_isEquipped);
        }
    }
}
