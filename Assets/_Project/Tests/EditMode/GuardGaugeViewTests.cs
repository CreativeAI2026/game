using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 防御ゲージの Point を何個点けるか(残量を個数で等分して切り上げる)の検証。
    /// 0 個になるのが空の時だけであることを、ここで保証する。
    /// </summary>
    public class GuardGaugeViewTests
    {
        private const int PointCount = 10;

        [TestCase(1f, 10)]
        [TestCase(0.95f, 10)]
        [TestCase(0.9f, 9)]
        [TestCase(0.85f, 9)]
        [TestCase(0.5f, 5)]
        [TestCase(0.1f, 1)]
        [TestCase(0.01f, 1)]
        [TestCase(0f, 0)]
        public void LitPoints_AreRatioRoundedUp(float ratio, int expected)
        {
            Assert.AreEqual(expected, GuardGaugeView.CountLitPoints(ratio, PointCount));
        }

        [Test]
        public void ExactSteps_DoNotLightOneExtraPoint()
        {
            // 0.1 刻みの割合は浮動小数の誤差で 9.0000004 などになるが、1 個多く点けない
            for (int step = 0; step <= PointCount; step++)
            {
                float ratio = step * 0.1f;
                Assert.AreEqual(
                    step,
                    GuardGaugeView.CountLitPoints(ratio, PointCount),
                    $"ratio={ratio}"
                );
            }
        }

        [TestCase(-0.5f, 0)]
        [TestCase(1.5f, 10)]
        public void OutOfRangeRatio_IsClamped(float ratio, int expected)
        {
            Assert.AreEqual(expected, GuardGaugeView.CountLitPoints(ratio, PointCount));
        }

        [Test]
        public void PointCount_SetsTheStep()
        {
            // 5 個なら 20% 刻み
            Assert.AreEqual(3, GuardGaugeView.CountLitPoints(0.5f, 5));
            Assert.AreEqual(2, GuardGaugeView.CountLitPoints(0.4f, 5));
        }
    }
}
