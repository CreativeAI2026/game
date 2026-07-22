using UnityEngine;

namespace CreativeAI.UI
{
    public readonly struct RevolverTabLayout
    {
        public RevolverTabLayout(
            Vector2 anchoredPosition,
            float scale,
            float alpha,
            bool isVisible,
            bool isInteractable,
            float relativePosition
        )
        {
            AnchoredPosition = anchoredPosition;
            Scale = scale;
            Alpha = alpha;
            IsVisible = isVisible;
            IsInteractable = isInteractable;
            RelativePosition = relativePosition;
        }

        public Vector2 AnchoredPosition { get; }
        public float Scale { get; }
        public float Alpha { get; }
        public bool IsVisible { get; }
        public bool IsInteractable { get; }
        public float RelativePosition { get; }
    }

    public static class RevolverTabLayoutCalculator
    {
        public static RevolverTabLayout Calculate(
            float relativePosition,
            RevolverTabLayoutSettings settings
        )
        {
            settings ??= new RevolverTabLayoutSettings();

            float visibleRadius = settings.VisibleRadius;
            float absoluteDistance = Mathf.Abs(relativePosition);
            bool isVisible = absoluteDistance <= visibleRadius + Mathf.Epsilon;
            float normalizedDistance = Mathf.Clamp01(absoluteDistance / visibleRadius);
            float angle = relativePosition / visibleRadius * settings.MaxAngle * Mathf.Deg2Rad;
            GetBasis(settings.Placement, out Vector2 tangent, out Vector2 inward);
            if (settings.ReverseOrder)
                tangent = -tangent;
            float tangentOffset = Mathf.Sin(angle) * settings.TangentRadius;
            float inwardOffset = (1f - Mathf.Cos(angle)) * settings.ArcDepth;
            Vector2 position = tangent * tangentOffset + inward * inwardOffset;

            float scaleT = EvaluateCurve(settings.ScaleCurve, normalizedDistance);
            float alphaT = EvaluateCurve(settings.AlphaCurve, normalizedDistance);
            float scale = Mathf.Lerp(settings.SelectedScale, settings.EdgeScale, scaleT);
            float alpha = isVisible
                ? Mathf.Lerp(settings.SelectedAlpha, settings.EdgeAlpha, alphaT)
                : 0f;

            return new RevolverTabLayout(
                position,
                Sanitize(scale),
                Mathf.Clamp01(Sanitize(alpha)),
                isVisible,
                isVisible,
                relativePosition
            );
        }

        private static float EvaluateCurve(AnimationCurve curve, float value)
        {
            float evaluated = curve != null ? curve.Evaluate(value) : value;
            return Mathf.Clamp01(Sanitize(evaluated));
        }

        private static void GetBasis(
            RevolverArcPlacement placement,
            out Vector2 tangent,
            out Vector2 inward
        )
        {
            switch (placement)
            {
                case RevolverArcPlacement.Top:
                    tangent = Vector2.right;
                    inward = Vector2.up;
                    break;
                case RevolverArcPlacement.Bottom:
                    tangent = Vector2.right;
                    inward = Vector2.down;
                    break;
                case RevolverArcPlacement.Left:
                    tangent = Vector2.down;
                    inward = Vector2.left;
                    break;
                default:
                    tangent = Vector2.down;
                    inward = Vector2.right;
                    break;
            }
        }

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
