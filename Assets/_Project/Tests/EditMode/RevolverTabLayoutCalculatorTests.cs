using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class RevolverTabLayoutCalculatorTests
    {
        private RevolverTabLayoutSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = new RevolverTabLayoutSettings
            {
                VisibleItemCount = 5,
                HorizontalRadius = 300f,
                VerticalRadius = 100f,
                MaxAngle = 90f,
                SelectedScale = 1f,
                EdgeScale = 0.5f,
                SelectedAlpha = 1f,
                EdgeAlpha = 0.2f,
            };
        }

        [Test]
        public void Center_HasMaximumScaleAndAlpha()
        {
            var center = RevolverTabLayoutCalculator.Calculate(0f, _settings);
            var edge = RevolverTabLayoutCalculator.Calculate(2f, _settings);

            Assert.AreEqual(1f, center.Scale, 0.0001f);
            Assert.AreEqual(1f, center.Alpha, 0.0001f);
            Assert.Greater(center.Scale, edge.Scale);
            Assert.Greater(center.Alpha, edge.Alpha);
        }

        [Test]
        public void Scale_IsNonIncreasingAsDistanceGrows()
        {
            float previous = float.PositiveInfinity;
            for (float distance = 0f; distance <= 2f; distance += 0.1f)
            {
                float scale = RevolverTabLayoutCalculator.Calculate(distance, _settings).Scale;
                Assert.LessOrEqual(scale, previous + 0.0001f);
                previous = scale;
            }
        }

        [Test]
        public void EqualLeftAndRightDistances_AreSymmetric()
        {
            var left = RevolverTabLayoutCalculator.Calculate(-1.25f, _settings);
            var right = RevolverTabLayoutCalculator.Calculate(1.25f, _settings);

            Assert.AreEqual(left.Scale, right.Scale, 0.0001f);
            Assert.AreEqual(left.Alpha, right.Alpha, 0.0001f);
            Assert.AreEqual(left.AnchoredPosition.y, right.AnchoredPosition.y, 0.0001f);
            Assert.AreEqual(-left.AnchoredPosition.x, right.AnchoredPosition.x, 0.0001f);
        }

        [Test]
        public void ArcDirection_ReversesVerticalPosition()
        {
            _settings.ArcDirection = RevolverTabArcDirection.Up;
            float up = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition.y;
            _settings.ArcDirection = RevolverTabArcDirection.Down;
            float down = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition.y;

            Assert.AreEqual(-up, down, 0.0001f);
        }

        [Test]
        public void OutsideRange_IsTransparentAndNotInteractable()
        {
            var layout = RevolverTabLayoutCalculator.Calculate(2.01f, _settings);

            Assert.IsFalse(layout.IsVisible);
            Assert.IsFalse(layout.IsInteractable);
            Assert.AreEqual(0f, layout.Alpha);
        }

        [Test]
        public void NullCurves_FallBackToLinearWithoutNaN()
        {
            _settings.ScaleCurve = null;
            _settings.AlphaCurve = null;
            var layout = RevolverTabLayoutCalculator.Calculate(1f, _settings);

            Assert.IsFalse(float.IsNaN(layout.Scale));
            Assert.IsFalse(float.IsNaN(layout.Alpha));
            Assert.AreEqual(0.75f, layout.Scale, 0.0001f);
            Assert.AreEqual(0.6f, layout.Alpha, 0.0001f);
        }
    }
}
