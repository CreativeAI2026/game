using System.Linq;
using CreativeAI.Crafting;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class CraftingStatRollerTests
    {
        private static CraftingStatRoller NewRoller() =>
            new CraftingStatRoller(CraftingParameters.Default);

        [Test]
        public void Roll_NeverProducesMoreThanTwoStats()
        {
            var roller = NewRoller();
            var a = StatVector.Of(
                (StatType.AttackPct, 10),
                (StatType.DefensePct, 8),
                (StatType.CritRate, 5)
            );
            var b = StatVector.Of((StatType.MaxHpPct, 6), (StatType.CritDamage, 4));

            for (int seed = 0; seed < 50; seed++)
            {
                var r = roller.Roll(a, b, new SystemRandomSource(seed));
                Assert.LessOrEqual(r.Count, 2, $"seed={seed} で付与数が2を超えた");
            }
        }

        [Test]
        public void Roll_OnlyAssignsStatsFromUnionOfParents()
        {
            // 結果の型は必ず親のいずれかが持つ型(ウェイト上位2)に含まれる
            var roller = NewRoller();
            var a = StatVector.Of((StatType.AttackPct, 10), (StatType.DefensePct, 1));
            var b = StatVector.Of((StatType.CritRate, 1));
            // ウェイト: Attack=10, Defense=1, CritRate=1 → 上位2 = {Attack, Defense}(同値は型順で Defense 優先)
            var allowed = new[] { StatType.AttackPct, StatType.DefensePct };

            for (int seed = 0; seed < 50; seed++)
            {
                var r = roller.Roll(a, b, new SystemRandomSource(seed));
                foreach (var t in r.Types)
                    Assert.Contains(t, allowed, $"seed={seed} で想定外の型 {t}");
            }
        }

        [Test]
        public void Roll_SingleSharedStat_GetsFullBudget()
        {
            // 候補が1型だけ(両親とも同じ型のみ)→ ロール不要で全量その型へ
            var roller = NewRoller();
            var a = StatVector.Of((StatType.AttackPct, 10));
            var b = StatVector.Of((StatType.AttackPct, 10));

            var r = roller.Roll(a, b, new SystemRandomSource(1));
            Assert.AreEqual(1, r.Count);
            Assert.Greater(r[StatType.AttackPct], 0f);

            // budget = base(10) + 弱い方の上乗せ。非劣化なので >= 10
            Assert.GreaterOrEqual(r[StatType.AttackPct], 10f);
        }

        [Test]
        public void Roll_IsDeterministic_ForSameSeed()
        {
            var roller = NewRoller();
            var a = StatVector.Of((StatType.AttackPct, 12), (StatType.CritDamage, 6));
            var b = StatVector.Of((StatType.DefensePct, 8), (StatType.AttackPct, 4));

            var r1 = roller.Roll(a, b, new SystemRandomSource(42));
            var r2 = roller.Roll(a, b, new SystemRandomSource(42));

            CollectionAssert.AreEquivalent(r1.Types, r2.Types);
            foreach (var t in r1.Types)
                Assert.AreEqual(r1[t], r2[t], 1e-6, $"型 {t} の値が再現しない");
        }

        [Test]
        public void Roll_TotalDoesNotExceedPowerCap()
        {
            var roller = NewRoller();
            var a = StatVector.Of((StatType.AttackPct, 60), (StatType.DefensePct, 30));
            var b = StatVector.Of((StatType.AttackPct, 50), (StatType.MaxHpPct, 40));

            for (int seed = 0; seed < 50; seed++)
            {
                var r = roller.Roll(a, b, new SystemRandomSource(seed));
                Assert.LessOrEqual(
                    r.Power,
                    CraftingParameters.Default.PowerCap + 1e-3,
                    $"seed={seed} で総量が上限を超えた"
                );
            }
        }

        [Test]
        public void Roll_ClampsCappedStat()
        {
            // 会心率は 100% で頭打ち。budget を会心率に全振りしても超えない。
            var roller = NewRoller();
            var a = StatVector.Of((StatType.CritRate, 80));
            var b = StatVector.Of((StatType.CritRate, 80));

            var r = roller.Roll(a, b, new SystemRandomSource(3));
            Assert.LessOrEqual(r[StatType.CritRate], 100f);
        }

        [Test]
        public void Roll_EmptyParents_ReturnsEmpty()
        {
            var roller = NewRoller();
            var r = roller.Roll(StatVector.Empty, StatVector.Empty, new SystemRandomSource(0));
            Assert.AreEqual(0, r.Count);
        }
    }
}
