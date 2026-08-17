using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class RecipeSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float SelectedSlotScale = 1.08f;
        private const float SelectedBounceHeight = 8f;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private SlotIconView _iconView;

        [SerializeField]
        private SlotHoverView _hoverView;

        [SerializeField]
        private SlotFrameView _frameView;

        private CraftRecipeData _recipe;
        private readonly HashSet<string> _warnedMissingViews = new();

        protected override SlotIconView IconView => _iconView;
        protected override SlotHoverView HoverView => _hoverView;
        protected override SlotFrameView FrameView => _frameView;

        public CraftRecipeData Recipe => _recipe;
        public event Action<RecipeSlot> Clicked;
        public event Action<RecipeSlot> DoubleClicked;

        protected override void Awake()
        {
            ResolveViewReferences();
            base.Awake();
            ConfigureHover();
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
            ResolveViewReferences();
            ConfigureHover();
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
            ResolveViewReferences();
            base.Refresh();
        }

        private void ConfigureHover()
        {
            ResolveViewReferences(false);
            if (_hoverView == null)
                return;

            _hoverView.Bind();
            _hoverView.SetGroup("recipe-slots");
            _hoverView.SetHoverScale(SelectedSlotScale);
            _hoverView.SetBounceHeight(SelectedBounceHeight);
            _hoverView.SetReleaseLockOnOutsideClick(false);
        }

        private void ResolveViewReferences(bool warn = true)
        {
            if (!warn)
                return;

            WarnIfMissing(_visualRootRect, "VisualRoot");
            WarnIfMissing(_iconView, nameof(SlotIconView));
            WarnIfMissing(_hoverView, nameof(SlotHoverView));
            WarnIfMissing(_frameView, nameof(SlotFrameView));
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            _iconView ??= GetComponentInChildren<SlotIconView>(true);
            _hoverView ??= GetComponentInChildren<SlotHoverView>(true);
            _frameView ??= GetComponentInChildren<SlotFrameView>(true);
        }
#endif

        private void WarnIfMissing(UnityEngine.Object reference, string referenceName)
        {
            if (reference != null || !_warnedMissingViews.Add(referenceName))
                return;

            Debug.LogWarning(
                $"{nameof(RecipeSlot)} '{name}' に {referenceName} がないため、該当表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
