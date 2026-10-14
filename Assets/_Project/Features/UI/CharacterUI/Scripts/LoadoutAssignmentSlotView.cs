using System;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using TMPro;
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
        private GameObject _filledPattern;

        [SerializeField]
        private GameObject _emptyPattern;

        [SerializeField]
        private TMP_Text _slotNumber;

        [SerializeField]
        private TMP_Text _emptyLabel;

        [SerializeField]
        private Sprite _normalBackgroundSprite;

        [SerializeField]
        private Sprite _selectedBackgroundSprite;

        [SerializeField, Range(0f, 1f)]
        private float _unavailableAlpha = 0.45f;

        [SerializeField, Range(0f, 1f)]
        private float _draggingAlpha = 0.45f;

        private Button _button;
        private ItemSlotDragOrigin _dragOrigin;
        private int _slotIndex = -1;
        private CanvasGroup _canvasGroup;
        private bool _available = true;
        private bool _selected;
        private bool _dragHovered;
        private bool _dragging;
        private bool _hasPreview;
        private ItemStack _previewStack;

        public ItemStack Stack { get; private set; }
        public event Action<LoadoutAssignmentSlotView> Clicked;
        public event Action<LoadoutAssignmentSlotView> DoubleClicked;
        public event Action<LoadoutAssignmentSlotView, ItemSlotDragPayload> Dropped;
        public event Action<LoadoutAssignmentSlotView, ItemSlotDragPayload> DropRejected;
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
            RefreshOpacity();
        }

        private void OnDisable()
        {
            ItemSlotDragContext.Ended -= OnDragEnded;
            FinishDragging();
            _dragHovered = false;
            RefreshBackground();
            RefreshOpacity();
        }

        public void ConfigureDragSource(ItemSlotDragOrigin origin, int slotIndex)
        {
            _dragOrigin = origin;
            _slotIndex = slotIndex;
            ResolveReferences();
            if (_slotNumber != null)
                _slotNumber.text = (slotIndex + 1).ToString();
            if (_emptyLabel != null)
                _emptyLabel.text = origin == ItemSlotDragOrigin.Quick ? "ここに登録" : "ここに装備";
            Refresh();
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

        public void SetAvailable(bool available)
        {
            _available = available;
            RefreshOpacity();
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

            _dragging = true;
            RefreshOpacity();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
                ItemSlotDragContext.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => FinishDragging();

        public void OnDrop(PointerEventData eventData)
        {
            if (ItemSlotDragContext.TryGet(out ItemSlotDragPayload payload))
            {
                if (CanAccept(payload))
                {
                    DragExited?.Invoke(this);
                    Dropped?.Invoke(this, payload);
                }
                else
                {
                    DropRejected?.Invoke(this, payload);
                }
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
            bool hasItem = displayedStack?.Data != null;

            _filledPattern?.SetActive(hasItem);
            _emptyPattern?.SetActive(!hasItem);

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.color = hasItem ? Color.white : Color.clear;
                _icon.preserveAspect = true;
            }
        }

        private void ResolveReferences()
        {
            if (_button == null)
                _button = GetComponent<Button>();
            if (_background == null)
                _background = GetComponent<Image>();
            if (_filledPattern == null)
                _filledPattern = transform.Find("FillPattern")?.gameObject;
            if (_emptyPattern == null)
                _emptyPattern =
                    transform.Find("EmptyPattern")?.gameObject
                    ?? transform.Find("EnptyPattern")?.gameObject;
            if (_icon == null)
                _icon = transform.Find("FillPattern/Icon")?.GetComponent<Image>();
            if (_slotNumber == null)
                _slotNumber = transform.Find("Superscript/Number")?.GetComponent<TMP_Text>();
            if (_emptyLabel == null)
                _emptyLabel =
                    transform.Find("EmptyPattern/label")?.GetComponent<TMP_Text>()
                    ?? transform.Find("EnptyPattern/label")?.GetComponent<TMP_Text>();
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
            {
                Sprite backgroundSprite =
                    _selected || _dragHovered ? _selectedBackgroundSprite : _normalBackgroundSprite;
                if (backgroundSprite != null)
                    _background.sprite = backgroundSprite;
                _background.color = Color.white;
            }
        }

        private void RefreshOpacity()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.alpha =
                _dragging ? _draggingAlpha
                : _available ? 1f
                : _unavailableAlpha;
        }

        private void FinishDragging()
        {
            if (!_dragging)
                return;

            _dragging = false;
            RefreshOpacity();
            ItemSlotDragContext.Clear();
        }

        private void OnDragEnded()
        {
            if (_dragging)
            {
                _dragging = false;
                RefreshOpacity();
            }

            ClearPreview();
            _dragHovered = false;
            RefreshBackground();
        }
    }
}
