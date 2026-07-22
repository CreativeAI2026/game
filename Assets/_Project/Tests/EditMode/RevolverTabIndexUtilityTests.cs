using CreativeAI.UI;
using NUnit.Framework;

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
    }
}
