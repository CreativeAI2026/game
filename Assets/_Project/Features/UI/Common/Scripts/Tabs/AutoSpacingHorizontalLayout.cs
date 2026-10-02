using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [AddComponentMenu("Layout/Auto Spacing Horizontal Layout")]
    public sealed class AutoSpacingHorizontalLayout : LayoutGroup
    {
        [SerializeField, Min(0f)]
        private float _minSpacing;

        [SerializeField, Min(0f)]
        private float _maxSpacing = 200f;

        public float MinSpacing
        {
            get => _minSpacing;
            set
            {
                SetProperty(ref _minSpacing, Mathf.Max(0f, value));
                SetProperty(ref _maxSpacing, Mathf.Max(_minSpacing, _maxSpacing));
            }
        }

        public float MaxSpacing
        {
            get => _maxSpacing;
            set => SetProperty(ref _maxSpacing, Mathf.Max(_minSpacing, value));
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            float childrenWidth = GetChildrenWidth();
            int gapCount = Mathf.Max(0, rectChildren.Count - 1);
            float minimumWidth = padding.horizontal + childrenWidth + _minSpacing * gapCount;
            float preferredWidth = padding.horizontal + childrenWidth + _maxSpacing * gapCount;

            SetLayoutInputForAxis(minimumWidth, preferredWidth, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float maxChildHeight = 0f;
            foreach (RectTransform child in rectChildren)
                maxChildHeight = Mathf.Max(maxChildHeight, child.rect.height);

            float height = padding.vertical + maxChildHeight;
            SetLayoutInputForAxis(height, height, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            if (rectChildren.Count == 0)
                return;

            float childrenWidth = GetChildrenWidth();
            int gapCount = rectChildren.Count - 1;
            float availableWidth = rectTransform.rect.width - padding.horizontal;
            float spacing =
                gapCount > 0
                    ? Mathf.Clamp(
                        (availableWidth - childrenWidth) / gapCount,
                        _minSpacing,
                        _maxSpacing
                    )
                    : 0f;
            float contentWidth = childrenWidth + spacing * gapCount;
            float position = GetStartOffset(0, contentWidth);

            foreach (RectTransform child in rectChildren)
            {
                SetChildAlongAxis(child, 0, position);
                position += GetChildWidth(child) + spacing;
            }
        }

        public override void SetLayoutVertical()
        {
            foreach (RectTransform child in rectChildren)
                SetChildAlongAxis(child, 1, GetStartOffset(1, child.rect.height));
        }

        protected override void OnValidate()
        {
            _minSpacing = Mathf.Max(0f, _minSpacing);
            _maxSpacing = Mathf.Max(_minSpacing, _maxSpacing);
            base.OnValidate();
        }

        private float GetChildrenWidth()
        {
            float width = 0f;
            foreach (RectTransform child in rectChildren)
                width += GetChildWidth(child);

            return width;
        }

        private static float GetChildWidth(RectTransform child) =>
            Mathf.Max(child.rect.width, LayoutUtility.GetPreferredWidth(child));
    }
}
