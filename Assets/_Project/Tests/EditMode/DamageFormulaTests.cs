using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 被ダメージの防御軽減式(最終ダメージ = 攻撃 × 500 / (500 + 防御))の検証。
    /// プレイヤーと敵の両方がこの式を使うので、ここが崩れると全戦闘のバランスが変わる。
    /// </summary>
    public class DamageFormulaTests
    {
        [Test]
        public void ZeroDefense_TakesFullDamage()
        {
            Assert.AreEqual(100f, DamageFormula.ApplyDefense(100f, 0f), 1e-4f);
        }

        [Test]
        public void DefenseEqualToConstant_HalvesDamage()
        {
            Assert.AreEqual(
                50f,
                DamageFormula.ApplyDefense(100f, DamageFormula.DefenseConstant),
                1e-4f
            );
        }

        [TestCase(100f, 500f, 50f)]
        [TestCase(100f, 1500f, 25f)]
        [TestCase(300f, 250f, 200f)]
        [TestCase(1000f, 100f, 833.3333f)]
        public void Damage_FollowsRatioFormula(float raw, float defense, float expected)
        {
            Assert.AreEqual(expected, DamageFormula.ApplyDefense(raw, defense), 1e-2f);
        }

        [Test]
        public void HigherDefense_NeverIncreasesDamage()
        {
            float previous = float.MaxValue;
            for (float defense = 0f; defense <= 5000f; defense += 100f)
            {
                float damage = DamageFormula.ApplyDefense(400f, defense);
                Assert.LessOrEqual(damage, previous, $"defense={defense}");
                previous = damage;
            }
        }

        [Test]
        public void NegativeDefense_IsTreatedAsZero()
        {
            Assert.AreEqual(
                DamageFormula.ApplyDefense(100f, 0f),
                DamageFormula.ApplyDefense(100f, -300f),
                1e-4f
            );
        }

        [TestCase(0f, 0f)]
        [TestCase(0.2f, 500f)]
        [TestCase(5f, 1_000_000f)]
        public void TinyAttackOrHugeDefense_StillDealsAtLeastOne(float raw, float defense)
        {
            Assert.AreEqual(1f, DamageFormula.ApplyDefense(raw, defense), 1e-4f);
        }
    }
}
