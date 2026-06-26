using System;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Button), typeof(Image))]
    public class RecipeSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float SelectedSlotScale = 1.08f;
        private const float SelectedBounceHeight = 8f;

        [SerializeField]
        private CraftRecipeData _recipe;

        private Image _frame;

        private static readonly Color SelectedColor = new Color(1f, 0.78f, 0.15f, 0.9f);
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.2f);

        public CraftRecipeData Recipe => _recipe;
        public event Action<RecipeSlot> Clicked;
        public event Action<RecipeSlot> DoubleClicked;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();

            ConfigureHoverScale();

            Refresh();
            SetSelected(false);
        }

        public void SetRecipe(CraftRecipeData recipe)
        {
            _recipe = recipe;
            _item = recipe?.resultItem;
            _count = recipe == null ? 0 : 1;
            Refresh();
        }

        public void RefreshDisplay()
        {
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            ResolveReferences();
            ConfigureHoverScale();

            if (_frame != null)
                _frame.color = selected ? SelectedColor : NormalColor;

            if (selected)
                Select();
            else
                Deselect();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _recipe == null)
                return;

            if (eventData.clickCount >= 2)
                DoubleClicked?.Invoke(this);
            else
                Clicked?.Invoke(this);
        }

        protected override void Refresh()
        {
            ResolveReferences();
            bool hasRecipe = _recipe != null && _item != null;

            if (_iconImage != null)
            {
                _iconImage.sprite = hasRecipe ? _item.icon : null;
                _iconImage.color = Color.white;
                _iconImage.gameObject.SetActive(hasRecipe && _item.icon != null);
            }

            if (_countText != null)
            {
                _countText.text = string.Empty;
                _countText.gameObject.SetActive(false);
            }
        }

        private void ResolveReferences()
        {
            InitializeBase();
            _frame ??= GetComponent<Image>();
        }

        private void ConfigureHoverScale()
        {
            if (_hoverScale == null)
                return;

            _hoverScale.SetGroup("recipe-slots");
            _hoverScale.SetHoverScale(SelectedSlotScale);
            _hoverScale.SetBounceHeight(SelectedBounceHeight);
            _hoverScale.SetReleaseLockOnOutsideClick(false);
            _hoverScale.SetTarget((RectTransform)transform);
            _hoverScale.SetBounceTarget(null);
        }
    }
}
