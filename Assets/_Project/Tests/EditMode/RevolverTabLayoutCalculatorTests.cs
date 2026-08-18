using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

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
                TangentRadius = 300f,
                ArcDepth = 100f,
                MaxAngle = 90f,
                Placement = RevolverArcPlacement.Top,
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
        [TestCase(RevolverArcPlacement.Top)]
        [TestCase(RevolverArcPlacement.Bottom)]
        [TestCase(RevolverArcPlacement.Left)]
        [TestCase(RevolverArcPlacement.Right)]
        public void AllPlacements_PutSelectedItemAtOrigin(RevolverArcPlacement placement)
        {
            _settings.Placement = placement;
            Assert.AreEqual(
                Vector2.zero,
                RevolverTabLayoutCalculator.Calculate(0f, _settings).AnchoredPosition
            );
        }

        [TestCase(RevolverArcPlacement.Top, 0f, 1f)]
        [TestCase(RevolverArcPlacement.Bottom, 0f, -1f)]
        [TestCase(RevolverArcPlacement.Left, -1f, 0f)]
        [TestCase(RevolverArcPlacement.Right, 1f, 0f)]
        public void Placement_OffsetsNeighborsTowardCircleCenter(
            RevolverArcPlacement placement,
            float expectedXSign,
            float expectedYSign
        )
        {
            _settings.Placement = placement;
            var negative = RevolverTabLayoutCalculator.Calculate(-1f, _settings);
            var positive = RevolverTabLayoutCalculator.Calculate(1f, _settings);

            if (expectedXSign != 0f)
            {
                Assert.AreEqual(expectedXSign, Mathf.Sign(negative.AnchoredPosition.x));
                Assert.AreEqual(expectedXSign, Mathf.Sign(positive.AnchoredPosition.x));
            }
            if (expectedYSign != 0f)
            {
                Assert.AreEqual(expectedYSign, Mathf.Sign(negative.AnchoredPosition.y));
                Assert.AreEqual(expectedYSign, Mathf.Sign(positive.AnchoredPosition.y));
            }
            Assert.AreEqual(negative.Scale, positive.Scale, 0.0001f);
            Assert.AreEqual(negative.Alpha, positive.Alpha, 0.0001f);
        }

        [Test]
        public void OppositePlacements_MirrorInwardAxis()
        {
            _settings.Placement = RevolverArcPlacement.Top;
            Vector2 top = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition;
            _settings.Placement = RevolverArcPlacement.Bottom;
            Vector2 bottom = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition;
            _settings.Placement = RevolverArcPlacement.Left;
            Vector2 left = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition;
            _settings.Placement = RevolverArcPlacement.Right;
            Vector2 right = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition;

            Assert.AreEqual(top.x, bottom.x, 0.0001f);
            Assert.AreEqual(-top.y, bottom.y, 0.0001f);
            Assert.AreEqual(left.y, right.y, 0.0001f);
            Assert.AreEqual(-left.x, right.x, 0.0001f);
            Assert.Less(left.y, 0f);
            Assert.Less(right.y, 0f);
        }

        [Test]
        public void ReverseOrder_OnlyFlipsTangentDirection()
        {
            _settings.Placement = RevolverArcPlacement.Top;
            Vector2 normal = RevolverTabLayoutCalculator.Calculate(1f, _settings).AnchoredPosition;
            _settings.ReverseOrder = true;
            Vector2 reversed = RevolverTabLayoutCalculator
                .Calculate(1f, _settings)
                .AnchoredPosition;

            Assert.AreEqual(-normal.x, reversed.x, 0.0001f);
            Assert.AreEqual(normal.y, reversed.y, 0.0001f);
        }

        [Test]
        public void FadeRange_ChangesAlphaAndScaleContinuously()
        {
            _settings.FadeEndDistance = 2.6f;
            _settings.HiddenScale = 0.3f;
            var edge = RevolverTabLayoutCalculator.Calculate(2f, _settings);
            var middle = RevolverTabLayoutCalculator.Calculate(2.3f, _settings);
            var end = RevolverTabLayoutCalculator.Calculate(2.6f, _settings);

            Assert.Greater(edge.Alpha, middle.Alpha);
            Assert.Greater(middle.Alpha, end.Alpha);
            Assert.Greater(edge.Scale, middle.Scale);
            Assert.Greater(middle.Scale, end.Scale);
            Assert.AreEqual(0f, end.Alpha);
            Assert.AreEqual(0.3f, end.Scale, 0.0001f);
            Assert.IsFalse(middle.IsInteractable);
            Assert.IsFalse(end.IsVisible);
        }

        [Test]
        public void WrappedBoundary_IsFullyHiddenBeforeSideChanges()
        {
            var beforeWrap = RevolverTabLayoutCalculator.Calculate(1.499f, _settings, 1.5f);
            var afterWrap = RevolverTabLayoutCalculator.Calculate(-1.499f, _settings, 1.5f);

            Assert.AreEqual(0f, beforeWrap.Alpha, 0.0001f);
            Assert.AreEqual(0f, afterWrap.Alpha, 0.0001f);
            Assert.AreEqual(_settings.HiddenScale, beforeWrap.Scale, 0.0001f);
            Assert.AreEqual(_settings.HiddenScale, afterWrap.Scale, 0.0001f);
            Assert.AreNotEqual(
                Mathf.Sign(beforeWrap.AnchoredPosition.x),
                Mathf.Sign(afterWrap.AnchoredPosition.x)
            );
        }

        [Test]
        public void VisibleMovement_DoesNotJumpToOppositeSide()
        {
            var first = RevolverTabLayoutCalculator.Calculate(1.1f, _settings, 1.5f);
            var second = RevolverTabLayoutCalculator.Calculate(1.2f, _settings, 1.5f);

            Assert.Greater(first.Alpha, 0f);
            Assert.Greater(second.Alpha, 0f);
            Assert.AreEqual(
                Mathf.Sign(first.AnchoredPosition.x),
                Mathf.Sign(second.AnchoredPosition.x)
            );
            Assert.Less(
                Vector2.Distance(first.AnchoredPosition, second.AnchoredPosition),
                _settings.TangentRadius
            );
        }

        [TestCase(RevolverArcPlacement.Top)]
        [TestCase(RevolverArcPlacement.Bottom)]
        [TestCase(RevolverArcPlacement.Left)]
        [TestCase(RevolverArcPlacement.Right)]
        public void EntryExit_WorksForAllPlacementsAndReverseOrder(RevolverArcPlacement placement)
        {
            _settings.Placement = placement;
            _settings.ReverseOrder = true;

            var entering = RevolverTabLayoutCalculator.Calculate(2.5f, _settings);
            var visible = RevolverTabLayoutCalculator.Calculate(2.25f, _settings);

            Assert.Greater(visible.Alpha, entering.Alpha);
            Assert.Greater(visible.Scale, entering.Scale);
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
