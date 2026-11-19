using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// HPを通常のバーとバフ分のゲージへ分ける計算の検証。
    /// バフ分は通常の HP の上に積まれ、ダメージはバフ分から先に減る。
    /// </summary>
    public class HpGaugeSplitTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void NoBonus_AllOnMainBar()
        {
            var split = HpGaugeSplit.Calculate(600f, 1000f, 0f);

            Assert.IsFalse(split.HasBonus);
            Assert.AreEqual(600f, split.MainHp, Tolerance);
            Assert.AreEqual(1000f, split.MainMaxHp, Tolerance);
            Assert.AreEqual(0f, split.BonusRatio, Tolerance);
        }

        [Test]
        public void FullHp_BothFull()
        {
            var split = HpGaugeSplit.Calculate(1500f, 1500f, 500f);

            Assert.IsTrue(split.HasBonus);
            Assert.AreEqual(1000f, split.MainHp, Tolerance);
            Assert.AreEqual(1000f, split.MainMaxHp, Tolerance);
            Assert.AreEqual(1f, split.BonusRatio, Tolerance);
        }

        [Test]
        public void Damage_ReducesBonusFirst()
        {
            var split = HpGaugeSplit.Calculate(1250f, 1500f, 500f);

            Assert.AreEqual(
                1000f,
                split.MainHp,
                Tolerance,
                "バフ分が残っている間は通常のバーは満タン"
            );
            Assert.AreEqual(0.5f, split.BonusRatio, Tolerance);
        }

        [Test]
        public void BonusEmpty_StillHasBonus()
        {
            var split = HpGaugeSplit.Calculate(400f, 1500f, 500f);

            Assert.IsTrue(split.HasBonus, "空になってもバフが付いている間はゲージを出す");
            Assert.AreEqual(0f, split.BonusRatio, Tolerance);
            Assert.AreEqual(400f, split.MainHp, Tolerance);
        }

        [Test]
        public void Dead_BothEmpty()
        {
            var split = HpGaugeSplit.Calculate(-50f, 1500f, 500f);

            Assert.AreEqual(0f, split.MainHp, Tolerance);
            Assert.AreEqual(0f, split.BonusRatio, Tolerance);
        }

        [Test]
        public void NegativeBonus_TreatedAsNoBonus()
        {
            // 最大HPが下がる装備のとき
            var split = HpGaugeSplit.Calculate(800f, 800f, -200f);

            Assert.IsFalse(split.HasBonus);
            Assert.AreEqual(800f, split.MainMaxHp, Tolerance);
            Assert.AreEqual(800f, split.MainHp, Tolerance);
        }
    }
}
