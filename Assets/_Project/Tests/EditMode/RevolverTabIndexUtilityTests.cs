using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine.EventSystems;

namespace CreativeAI.Tests.EditMode
{
    public class RevolverTabIndexUtilityTests
    {
        [TestCase(-1, 5, 4)]
        [TestCase(-6, 5, 4)]
        [TestCase(7, 5, 2)]
        [TestCase(int.MaxValue, 2, 1)]
        [TestCase(10, 1, 0)]
        [TestCase(0, 0, -1)]
        public void WrapIndex_ReturnsExpectedValue(int index, int count, int expected)
        {
            Assert.AreEqual(expected, RevolverTabIndexUtility.WrapIndex(index, count));
        }

        [TestCase(4, 0f, 5, -1f)]
        [TestCase(0, 4f, 5, 1f)]
        [TestCase(0, 1.5f, 5, -1.5f)]
        [TestCase(4, 0.5f, 5, -1.5f)]
        [TestCase(2, 0f, 4, 2f)]
        public void SignedWrappedDistance_UsesShortestContinuousDistance(
            int item,
            float selection,
            int count,
            float expected
        )
        {
            Assert.AreEqual(
                expected,
                RevolverTabIndexUtility.SignedWrappedDistance(item, selection, count),
                0.0001f
            );
        }

        [TestCase(0, 4, 5, -1)]
        [TestCase(4, 0, 5, 1)]
        [TestCase(0, 2, 4, 2)]
        [TestCase(2, 0, 4, 2)]
        [TestCase(0, 0, 1, 0)]
        public void ShortestStep_UsesLoopAndPositiveEvenTie(
            int from,
            int to,
            int count,
            int expected
        )
        {
            Assert.AreEqual(expected, RevolverTabIndexUtility.ShortestStep(from, to, count));
        }

        [TestCase(RevolverArcPlacement.Top)]
        [TestCase(RevolverArcPlacement.Bottom)]
        public void HorizontalPlacements_ResolveOnlyLeftAndRight(RevolverArcPlacement placement)
        {
            AssertStep(placement, false, MoveDirection.Left, -1);
            AssertStep(placement, false, MoveDirection.Right, 1);
            AssertInvalid(placement, MoveDirection.Up);
            AssertInvalid(placement, MoveDirection.Down);
        }

        [TestCase(RevolverArcPlacement.Left)]
        [TestCase(RevolverArcPlacement.Right)]
        public void VerticalPlacements_ResolveOnlyUpAndDown(RevolverArcPlacement placement)
        {
            AssertStep(placement, false, MoveDirection.Up, -1);
            AssertStep(placement, false, MoveDirection.Down, 1);
            AssertInvalid(placement, MoveDirection.Left);
            AssertInvalid(placement, MoveDirection.Right);
        }

        [TestCase(RevolverArcPlacement.Top, MoveDirection.Left, 1)]
        [TestCase(RevolverArcPlacement.Top, MoveDirection.Right, -1)]
        [TestCase(RevolverArcPlacement.Bottom, MoveDirection.Left, 1)]
        [TestCase(RevolverArcPlacement.Bottom, MoveDirection.Right, -1)]
        [TestCase(RevolverArcPlacement.Left, MoveDirection.Up, 1)]
        [TestCase(RevolverArcPlacement.Left, MoveDirection.Down, -1)]
        [TestCase(RevolverArcPlacement.Right, MoveDirection.Up, 1)]
        [TestCase(RevolverArcPlacement.Right, MoveDirection.Down, -1)]
        public void ReverseOrder_FlipsResolvedStep(
            RevolverArcPlacement placement,
            MoveDirection direction,
            int expectedStep
        )
        {
            AssertStep(placement, true, direction, expectedStep);
        }

        private static void AssertStep(
            RevolverArcPlacement placement,
            bool reverseOrder,
            MoveDirection direction,
            int expected
        )
        {
            Assert.IsTrue(
                RevolverTabNavigationUtility.TryResolveNavigationStep(
                    placement,
                    reverseOrder,
                    direction,
                    out int step
                )
            );
            Assert.AreEqual(expected, step);
        }

        private static void AssertInvalid(RevolverArcPlacement placement, MoveDirection direction)
        {
            Assert.IsFalse(
                RevolverTabNavigationUtility.TryResolveNavigationStep(
                    placement,
                    false,
                    direction,
                    out int step
                )
            );
            Assert.AreEqual(0, step);
        }
    }
}
