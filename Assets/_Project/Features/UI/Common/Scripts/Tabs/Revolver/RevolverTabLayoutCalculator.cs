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
            float direction = settings.ArcDirection == RevolverTabArcDirection.Up ? 1f : -1f;
            var position = new Vector2(
                Mathf.Sin(angle) * settings.HorizontalRadius,
                (Mathf.Cos(angle) - 1f) * settings.VerticalRadius * direction
            );

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

        private static float Sanitize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}
