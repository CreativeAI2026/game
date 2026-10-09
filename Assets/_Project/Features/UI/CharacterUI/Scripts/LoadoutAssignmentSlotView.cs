using System;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    /// <summary>
    /// Loadout画面のクイックアイテム・装備スロット共通表示。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class LoadoutAssignmentSlotView
        : MonoBehaviour,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            IDropHandler,
            IPointerEnterHandler,
            IPointerExitHandler
    {
        [SerializeField]
        private Image _background;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Color _normalColor = new(0f, 0f, 0f, 0.39f);

        [SerializeField]
        private Color _selectedColor = new(0.31f, 0.23f, 0.12f, 0.9f);

        private Button _button;
        private ItemSlotDragOrigin _dragOrigin;
        private int _slotIndex = -1;
        private CanvasGroup _dragCanvasGroup;
        private float _dragOriginalAlpha = 1f;
        private bool _selected;
        private bool _dragHovered;
        private bool _dragging;
        private bool _hasPreview;
        private ItemStack _previewStack;

        public ItemStack Stack { get; private set; }
        public event Action<LoadoutAssignmentSlotView> Clicked;
        public event Action<LoadoutAssignmentSlotView> DoubleClicked;
        public event Action<LoadoutAssignmentSlotView, ItemSlotDragPayload> Dropped;
        public event Action<LoadoutAssignmentSlotView, ItemSlotDragPayload> DragEntered;
        public event Action<LoadoutAssignmentSlotView> DragExited;

        private void Awake()
        {
            ResolveReferences();
            Refresh();
        }

        private void OnEnable()
        {
            ItemSlotDragContext.Ended += OnDragEnded;
            RefreshBackground();
        }

        private void OnDisable()
        {
            ItemSlotDragContext.Ended -= OnDragEnded;
            FinishDragging();
            _dragHovered = false;
            RefreshBackground();
        }

        public void ConfigureDragSource(ItemSlotDragOrigin origin, int slotIndex)
        {
            _dragOrigin = origin;
            _slotIndex = slotIndex;
        }

        public void SetStack(ItemStack stack)
        {
            Stack = stack;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshBackground();
        }

        public void ShowPreview(ItemStack stack)
        {
            _previewStack = stack;
            _hasPreview = true;
            Refresh();
        }

        public void ClearPreview()
        {
            if (!_hasPreview)
                return;

            _previewStack = null;
            _hasPreview = false;
            Refresh();
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

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || Stack?.Data == null)
                return;

            if (!ItemSlotDragContext.Begin(Stack, _dragOrigin, this, eventData, _slotIndex))
                return;

            _dragCanvasGroup ??=
                GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
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

        public void OnDrop(PointerEventData eventData)
        {
            if (ItemSlotDragContext.TryGet(out ItemSlotDragPayload payload) && CanAccept(payload))
            {
                DragExited?.Invoke(this);
                Dropped?.Invoke(this, payload);
            }

            _dragHovered = false;
            RefreshBackground();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _dragHovered =
                ItemSlotDragContext.TryGet(out ItemSlotDragPayload payload) && CanAccept(payload);
            if (_dragHovered)
                DragEntered?.Invoke(this, payload);
            RefreshBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragHovered)
                DragExited?.Invoke(this);
            _dragHovered = false;
            RefreshBackground();
        }

        private void Refresh()
        {
            ResolveReferences();
            ItemStack displayedStack = _hasPreview ? _previewStack : Stack;
            Sprite sprite = displayedStack?.Data?.icon;
            if (_icon == null)
                return;

            _icon.sprite = sprite;
            _icon.color = sprite != null ? Color.white : Color.clear;
            _icon.preserveAspect = true;
        }

        private void ResolveReferences()
        {
            _button ??= GetComponent<Button>();
            _background ??= GetComponent<Image>();
            _icon ??= transform.Find("Icon")?.GetComponent<Image>();
        }

        private bool CanAccept(ItemSlotDragPayload payload)
        {
            return _dragOrigin switch
            {
                ItemSlotDragOrigin.Quick => payload.Stack?.Data?.category == ItemCategory.Food
                    && payload.Origin is ItemSlotDragOrigin.Inventory or ItemSlotDragOrigin.Quick,
                ItemSlotDragOrigin.Equipment => payload.Stack?.Data is EquipmentData
                    && payload.Origin
                        is ItemSlotDragOrigin.Inventory
                            or ItemSlotDragOrigin.Equipment,
                _ => false,
            };
        }

        private void RefreshBackground()
        {
            ResolveReferences();
            if (_background != null)
                _background.color = _selected || _dragHovered ? _selectedColor : _normalColor;
        }

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
            if (_dragging)
            {
                if (_dragCanvasGroup != null)
                    _dragCanvasGroup.alpha = _dragOriginalAlpha;
                _dragging = false;
            }

            ClearPreview();
            _dragHovered = false;
            RefreshBackground();
        }
    }
}
