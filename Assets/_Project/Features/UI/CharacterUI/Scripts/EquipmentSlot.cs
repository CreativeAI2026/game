using System;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button)), RequireComponent(typeof(Image))]
    public class EquipmentSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float SelectedIconScale = 1.08f;
        private const float IconPadding = 14f;
        private const float EmptyIconAlpha = 50f / 255f;

        public Button Button { get; private set; }
        public event Action<EquipmentSlot> DoubleClicked;

        private TMP_Text _emptyText;
        private Image _frame;
        private RectTransform _visualRootRect;
        private RectTransform _numberSlotRect;
        private CanvasGroup _numberSlotCanvasGroup;
        private Image _numberSlotImage;
        private bool _inputLocked;

        public new ItemData Item
        {
            get => _item;
            set => SetItem(value, FindInventoryCount(value));
        }

        public void Init()
        {
            PreferChildIconImage();
            InitializeBase();

            Button = GetComponent<Button>();
            _emptyText = transform.Find("EmptyText")?.GetComponent<TMP_Text>();
            _frame = GetComponent<Image>();

            ResolveVisualReferences();

            if (_hoverScale != null)
            {
                _hoverScale.SetGroup("equipment-slots");
                _hoverScale.SetHoverScale(SelectedIconScale);
                _hoverScale.SetBounceHeight(0f);
                _hoverScale.SetReleaseLockOnOutsideClick(false);
            }

            ApplyIconPadding();
            BindSlotHoverTarget();
            Refresh();
        }

        public override void SetItem(ItemData item, int count = 1)
        {
            base.SetItem(item, count);
            ResolveVisualReferences();
            ApplyNumberSlotState();
        }

        protected override void Refresh()
        {
            base.Refresh();

            ResolveVisualReferences();
            ApplyIconState();
            ApplyNumberSlotState();
            BindSlotHoverTarget();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(_item == null || _item.icon == null);
        }

        public override void Clear()
        {
            base.Clear();

            ResolveVisualReferences();
            ApplyIconState();
            ApplyNumberSlotState();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(true);
        }

        public void UpdateCount()
        {
            SetCount(FindInventoryCount(_item));
        }

        public void EquipAnimated(ItemData item)
        {
            SetItemAnimated(item, FindInventoryCount(item));
        }

        public void SetFrameColor(Color color)
        {
            if (_frame != null)
                _frame.color = color;
        }

        public void SetSelected(bool selected)
        {
            if (selected)
                Select();
            else
                Deselect();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_inputLocked)
                return;

            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
                DoubleClicked?.Invoke(this);
        }

        private static int FindInventoryCount(ItemData item)
        {
            if (item == null)
                return 0;

            var stack = InventoryManager.Instance?.GetAllItems().Find(s => s.Data == item);
            return stack?.Count ?? 1;
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

            bool equipped = _item != null && _item.icon != null;
            if (equipped && _iconImage.sprite != _item.icon)
                _iconImage.sprite = _item.icon;

            _iconImage.gameObject.SetActive(true);
            _iconImage.color = new Color(1f, 1f, 1f, equipped ? 1f : EmptyIconAlpha);
        }

        private void PreferChildIconImage()
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Icon")
                {
                    _iconImage = image;
                    return;
                }
            }
        }

        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;

            if (Button != null)
                Button.interactable = !locked;
        }

        private void ApplyNumberSlotState()
        {
            ResolveVisualReferences();

            if (_numberSlotRect == null)
                return;

            bool hasItem = _item != null;
            _numberSlotRect.gameObject.SetActive(hasItem);
            if (_numberSlotCanvasGroup != null)
                _numberSlotCanvasGroup.alpha = hasItem ? 1f : 0f;
            if (_numberSlotImage != null)
            {
                _numberSlotImage.enabled = true;
                _numberSlotImage.color = new Color32(0, 0, 0, 200);
            }
        }

        private void ResolveVisualReferences()
        {
            if (_visualRootRect == null)
                _visualRootRect = FindChildRectIgnoreCase("VisualRoot");
            if (_visualRootRect == null)
                _visualRootRect = CreateVisualRoot();

            if (_numberSlotRect == null)
                _numberSlotRect = FindNumberSlotRect();
            if (_numberSlotRect == null)
                _numberSlotRect = CreateNumberSlot();

            if (_numberSlotRect == null)
                return;

            _numberSlotCanvasGroup ??= _numberSlotRect.GetComponent<CanvasGroup>();
            if (_numberSlotCanvasGroup == null)
                _numberSlotCanvasGroup = _numberSlotRect.gameObject.AddComponent<CanvasGroup>();

            _numberSlotCanvasGroup.interactable = false;
            _numberSlotCanvasGroup.blocksRaycasts = false;

            _numberSlotImage ??= _numberSlotRect.GetComponent<Image>();
            if (_numberSlotImage == null)
                _numberSlotImage = _numberSlotRect.gameObject.AddComponent<Image>();

            _numberSlotImage.raycastTarget = false;
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

        private RectTransform FindNumberSlotRect()
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (!string.Equals(rect.name, "numberSlot", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rect.GetComponent<TMP_Text>() != null)
                    continue;

                return rect;
            }

            if (_countText != null)
                return _countText.transform.parent as RectTransform;

            return null;
        }

        private RectTransform CreateVisualRoot()
        {
            var visualRootObject = new GameObject("VisualRoot", typeof(RectTransform));
            var rect = visualRootObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * IconPadding;
            rect.offsetMax = -Vector2.one * IconPadding;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            if (_iconImage != null)
            {
                _iconImage.rectTransform.SetParent(rect, false);
                _iconImage.rectTransform.SetAsFirstSibling();
            }

            return rect;
        }

        private void ApplyVisualRootPadding()
        {
            if (_visualRootRect == null)
                return;

            _visualRootRect.anchorMin = Vector2.zero;
            _visualRootRect.anchorMax = Vector2.one;
            _visualRootRect.offsetMin = Vector2.one * IconPadding;
            _visualRootRect.offsetMax = -Vector2.one * IconPadding;
            _visualRootRect.pivot = new Vector2(0.5f, 0.5f);
        }

        private RectTransform CreateNumberSlot()
        {
            if (_visualRootRect == null)
                _visualRootRect = FindChildRectIgnoreCase("VisualRoot") ?? CreateVisualRoot();
            if (_visualRootRect == null)
                return null;

            var numberSlotObject = new GameObject(
                "numberSlot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup)
            );
            var numberSlotRect = numberSlotObject.GetComponent<RectTransform>();
            numberSlotRect.SetParent(_visualRootRect, false);
            numberSlotRect.anchorMin = new Vector2(0f, 0f);
            numberSlotRect.anchorMax = new Vector2(1f, 0f);
            numberSlotRect.pivot = new Vector2(0.5f, 0f);
            numberSlotRect.anchoredPosition = Vector2.zero;
            numberSlotRect.sizeDelta = new Vector2(0f, 40f);
            numberSlotRect.localScale = Vector3.one;

            _numberSlotImage = numberSlotObject.GetComponent<Image>();
            _numberSlotImage.color = new Color32(0, 0, 0, 200);
            _numberSlotImage.raycastTarget = false;

            _numberSlotCanvasGroup = numberSlotObject.GetComponent<CanvasGroup>();
            _numberSlotCanvasGroup.interactable = false;
            _numberSlotCanvasGroup.blocksRaycasts = false;

            CreateNumberSlotText(numberSlotRect);

            return numberSlotRect;
        }

        private void CreateNumberSlotText(RectTransform parent)
        {
            if (_countText != null)
            {
                _countText.rectTransform.SetParent(parent, false);
                _countText.name = "Text";
            }
            else
            {
                var textObject = new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI),
                    typeof(CanvasGroup)
                );
                textObject.transform.SetParent(parent, false);
                _countText = textObject.GetComponent<TextMeshProUGUI>();
            }

            var textRect = _countText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.localScale = Vector3.one;

            _countText.color = Color.white;
            _countText.enableAutoSizing = true;
            _countText.fontSize = 40f;
            _countText.fontSizeMin = Mathf.Max(_countText.fontSizeMin, 18f);
            _countText.fontSizeMax = Mathf.Max(_countText.fontSizeMax, 48f);
            _countText.alignment = TextAlignmentOptions.Center;
            _countText.raycastTarget = false;
        }
    }
}
