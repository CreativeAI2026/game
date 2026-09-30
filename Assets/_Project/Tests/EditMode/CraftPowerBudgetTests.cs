using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合ステータスの総量計算(ソフトキャップ / ハードキャップ)の検証。
    /// </summary>
    public class CraftPowerBudgetTests
    {
        private static readonly StatRollParameters P = new StatRollParameters
        {
            Beta = 0.5,
            PowerCap = 100.0,
        };

        [Test]
        public void SoftCap_NeverDegrades_ResultAtLeastStrongerParent()
        {
            // B ≥ max(powerA, powerB)(非劣化保証)
            double b = RecipeCraftingService.ComputePowerBudget(40, 20, P);
            Assert.GreaterOrEqual(b, 40.0);
        }

        [Test]
        public void SoftCap_StaysBelowCap_WhenBaseUnderCap()
        {
            // base < cap のとき B < cap(上限に漸近・張り付かない)
            double b = RecipeCraftingService.ComputePowerBudget(40, 90, P);
            Assert.Less(b, P.PowerCap);
            Assert.Greater(b, 90.0); // 弱い方の分は伸びている
        }

        [Test]
        public void SoftCap_IsSymmetric()
        {
            Assert.AreEqual(
                RecipeCraftingService.ComputePowerBudget(30, 70, P),
                RecipeCraftingService.ComputePowerBudget(70, 30, P),
                1e-9
            );
        }

        [Test]
        public void SoftCap_WithZeroWeakParent_EqualsBase()
        {
            // sub = 0 → 伸びしろを埋めないので B = base
            double b = RecipeCraftingService.ComputePowerBudget(55, 0, P);
            Assert.AreEqual(55.0, b, 1e-9);
        }

        [Test]
        public void SoftCap_WhenBaseAtOrAboveCap_ReturnsBase()
        {
            // 既に上限以上なら成長させない(発散防止)
            double b = RecipeCraftingService.ComputePowerBudget(120, 30, P);
            Assert.AreEqual(120.0, b, 1e-9);
        }
    }
}
