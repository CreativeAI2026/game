using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CreativeAI.UI
{
    [Obsolete("Use RevolverArcPlacement instead.")]
    public enum RevolverTabArcDirection
    {
        Up,
        Down,
    }

    public enum RevolverArcPlacement
    {
        Top,
        Bottom,
        Left,
        Right,
    }

    [Serializable]
    public sealed class RevolverTabLayoutSettings
    {
        [SerializeField, Min(1)]
        private int _visibleItemCount = 5;

        [FormerlySerializedAs("_horizontalRadius")]
        [SerializeField, Min(0f)]
        private float _tangentRadius = 300f;

        [FormerlySerializedAs("_verticalRadius")]
        [SerializeField, Min(0f)]
        private float _arcDepth = 120f;

        [SerializeField, Range(0f, 180f)]
        private float _maxAngle = 90f;

        [SerializeField]
        private RevolverArcPlacement _placement = RevolverArcPlacement.Bottom;

        [SerializeField]
        private bool _reverseOrder;

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

        public float TangentRadius
        {
            get => _tangentRadius;
            set => _tangentRadius = Mathf.Max(0f, value);
        }

        [Obsolete("Use TangentRadius instead.")]
        public float HorizontalRadius
        {
            get => TangentRadius;
            set => TangentRadius = value;
        }

        public float ArcDepth
        {
            get => _arcDepth;
            set => _arcDepth = Mathf.Max(0f, value);
        }

        [Obsolete("Use ArcDepth instead.")]
        public float VerticalRadius
        {
            get => ArcDepth;
            set => ArcDepth = value;
        }

        public float MaxAngle
        {
            get => _maxAngle;
            set => _maxAngle = Mathf.Clamp(value, 0f, 180f);
        }

        public RevolverArcPlacement Placement
        {
            get => _placement;
            set => _placement = value;
        }

        [Obsolete("Use Placement instead.")]
        public RevolverTabArcDirection ArcDirection
        {
            get =>
                Placement == RevolverArcPlacement.Top
                    ? RevolverTabArcDirection.Up
                    : RevolverTabArcDirection.Down;
            set =>
                Placement =
                    value == RevolverTabArcDirection.Up
                        ? RevolverArcPlacement.Top
                        : RevolverArcPlacement.Bottom;
        }

        public bool ReverseOrder
        {
            get => _reverseOrder;
            set => _reverseOrder = value;
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
