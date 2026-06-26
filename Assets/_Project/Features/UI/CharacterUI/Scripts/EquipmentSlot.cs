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

        public new ItemData Item
        {
            get => _item;
            set => SetItem(value, FindInventoryCount(value));
        }

        public void Init()
        {
            InitializeBase();
            Button = GetComponent<Button>();
            _emptyText = transform.Find("EmptyText")?.GetComponent<TMP_Text>();
            _frame = GetComponent<Image>();

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

        protected override void Refresh()
        {
            base.Refresh();
            ApplyIconState();
            BindSlotHoverTarget();

            if (_emptyText != null)
                _emptyText.gameObject.SetActive(_item == null || _item.icon == null);
        }

        public override void Clear()
        {
            base.Clear();
            ApplyIconState();

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
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.one * IconPadding;
            iconRect.offsetMax = -Vector2.one * IconPadding;
        }

        private void BindSlotHoverTarget()
        {
            if (_hoverScale == null)
                return;

            _hoverScale.SetTarget((RectTransform)transform);
            _hoverScale.SetBounceTarget(null);
        }

        private void ApplyIconState()
        {
            if (_iconImage == null)
                return;

            bool equipped = _item != null && _item.icon != null;
            _iconImage.gameObject.SetActive(true);
            _iconImage.color = new Color(1f, 1f, 1f, equipped ? 1f : EmptyIconAlpha);
        }
    }
}
