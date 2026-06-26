using System;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class MaterialSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float SelectedSlotScale = 1.08f;
        private const float IconPadding = 14f;
        private const float EmptyIconAlpha = 50f / 255f;

        private static readonly Color SelectedFrameColor = new Color(1f, 0.78f, 0.15f, 0.9f);
        private static readonly Color NormalFrameColor = new Color(1f, 1f, 1f, 0.2f);

        private TMP_Text _emptyText;
        private Image _frame;

        public event Action<MaterialSlot> Clicked;
        public event Action<MaterialSlot> DoubleClicked;

        protected override void Awake()
        {
            base.Awake();
            _emptyText = transform.Find("EmptyText")?.GetComponent<TMP_Text>();
            _frame = GetComponent<Image>();

            if (_hoverScale != null)
            {
                _hoverScale.SetGroup("craft-slots");
                _hoverScale.SetHoverScale(SelectedSlotScale);
                _hoverScale.SetBounceHeight(0f);
                _hoverScale.SetReleaseLockOnOutsideClick(false);
            }

            ApplyIconPadding();
            BindSlotHoverTarget();
            Clear();
            SetSelected(false);
        }

        public void SetMaterial(ItemData item, int count)
        {
            SetItem(item, count);
        }

        public void SetMaterialAnimated(ItemData item, int count)
        {
            SetItemAnimated(item, count);
        }

        public void SetSelected(bool selected)
        {
            if (_frame != null)
                _frame.color = selected ? SelectedFrameColor : NormalFrameColor;

            if (selected)
                Select();
            else
                Deselect();
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

            bool hasMaterial = _item != null && _item.icon != null;
            _iconImage.gameObject.SetActive(true);
            _iconImage.color = new Color(1f, 1f, 1f, hasMaterial ? 1f : EmptyIconAlpha);
        }
    }
}
