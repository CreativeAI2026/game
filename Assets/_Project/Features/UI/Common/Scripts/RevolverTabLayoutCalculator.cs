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
            RevolverTabLayoutSettings settings,
            float wrapDistance = float.PositiveInfinity
        )
        {
            settings ??= new RevolverTabLayoutSettings();

            float configuredVisibleEdge = settings.VisibleEdgeDistance;
            float fadeEnd = settings.FadeEndDistance;
            if (!float.IsInfinity(wrapDistance))
                fadeEnd = Mathf.Min(fadeEnd, Mathf.Max(0f, wrapDistance - 0.001f));
            float visibleEdge = configuredVisibleEdge;
            if (visibleEdge >= fadeEnd)
                visibleEdge = Mathf.Max(0f, fadeEnd - Mathf.Min(0.6f, fadeEnd * 0.5f));
            float absoluteDistance = Mathf.Abs(relativePosition);
            bool isVisible = absoluteDistance < fadeEnd;
            bool isInteractable = absoluteDistance <= visibleEdge && isVisible;
            float normalizedDistance = Mathf.Clamp01(
                absoluteDistance / Mathf.Max(Mathf.Epsilon, visibleEdge)
            );
            float fadeT = Mathf.InverseLerp(visibleEdge, fadeEnd, absoluteDistance);
            float normalAngle = normalizedDistance * settings.MaxAngle;
            float exitAngle =
                settings.MaxAngle + settings.ExitAnglePadding * EvaluateCurve(null, fadeT);
            float angle =
                Mathf.Sign(relativePosition)
                * Mathf.Lerp(normalAngle, exitAngle, fadeT)
                * Mathf.Deg2Rad;
            GetBasis(settings.Placement, out Vector2 tangent, out Vector2 inward);
            if (settings.ReverseOrder)
                tangent = -tangent;
            float tangentOffset = Mathf.Sin(angle) * settings.TangentRadius;
            float inwardOffset = (1f - Mathf.Cos(angle)) * settings.ArcDepth;
            Vector2 position = tangent * tangentOffset + inward * inwardOffset;

            float scaleT = EvaluateCurve(settings.ScaleCurve, normalizedDistance);
            float alphaT = EvaluateCurve(settings.AlphaCurve, normalizedDistance);
            float normalScale = Mathf.Lerp(settings.SelectedScale, settings.EdgeScale, scaleT);
            float normalAlpha = Mathf.Lerp(settings.SelectedAlpha, settings.EdgeAlpha, alphaT);
            float scale = Mathf.Lerp(
                normalScale,
                settings.HiddenScale,
                EvaluateCurve(settings.EntryExitScaleCurve, fadeT)
            );
            float alpha = Mathf.Lerp(
                normalAlpha,
                0f,
                EvaluateCurve(settings.EntryExitAlphaCurve, fadeT)
            );
            if (!isVisible)
            {
                scale = settings.HiddenScale;
                alpha = 0f;
            }

            return new RevolverTabLayout(
                position,
                Sanitize(scale),
                Mathf.Clamp01(Sanitize(alpha)),
                isVisible,
                isInteractable,
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
