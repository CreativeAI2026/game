using System;
using UnityEngine;

namespace CreativeAI.UI
{
    public enum RevolverTabArcDirection
    {
        Up,
        Down,
    }

    [Serializable]
    public sealed class RevolverTabLayoutSettings
    {
        [SerializeField, Min(1)]
        private int _visibleItemCount = 5;

        [SerializeField, Min(0f)]
        private float _horizontalRadius = 300f;

        [SerializeField, Min(0f)]
        private float _verticalRadius = 120f;

        [SerializeField, Range(0f, 180f)]
        private float _maxAngle = 90f;

        [SerializeField]
        private RevolverTabArcDirection _arcDirection = RevolverTabArcDirection.Up;

        [SerializeField, Min(0f)]
        private float _selectedScale = 1.2f;

        [SerializeField, Min(0f)]
        private float _edgeScale = 0.55f;

        [SerializeField]
        private AnimationCurve _scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float _selectedAlpha = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _edgeAlpha = 0.4f;

        [SerializeField]
        private AnimationCurve _alphaCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public int VisibleItemCount
        {
            get => _visibleItemCount;
            set => _visibleItemCount = Mathf.Max(1, value);
        }

        public float HorizontalRadius
        {
            get => _horizontalRadius;
            set => _horizontalRadius = Mathf.Max(0f, value);
        }

        public float VerticalRadius
        {
            get => _verticalRadius;
            set => _verticalRadius = Mathf.Max(0f, value);
        }

        public float MaxAngle
        {
            get => _maxAngle;
            set => _maxAngle = Mathf.Clamp(value, 0f, 180f);
        }

        public RevolverTabArcDirection ArcDirection
        {
            get => _arcDirection;
            set => _arcDirection = value;
        }

        public float SelectedScale
        {
            get => _selectedScale;
            set => _selectedScale = Mathf.Max(0f, value);
        }

        public float EdgeScale
        {
            get => _edgeScale;
            set => _edgeScale = Mathf.Max(0f, value);
        }

        public AnimationCurve ScaleCurve
        {
            get => _scaleCurve;
            set => _scaleCurve = value;
        }

        public float SelectedAlpha
        {
            get => _selectedAlpha;
            set => _selectedAlpha = Mathf.Clamp01(value);
        }

        public float EdgeAlpha
        {
            get => _edgeAlpha;
            set => _edgeAlpha = Mathf.Clamp01(value);
        }

        public AnimationCurve AlphaCurve
        {
            get => _alphaCurve;
            set => _alphaCurve = value;
        }

        public float VisibleRadius => Mathf.Max(0.5f, (VisibleItemCount - 1) * 0.5f);
    }
}
