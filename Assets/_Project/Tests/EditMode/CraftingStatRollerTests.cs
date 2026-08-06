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
            // 結果の型は必ず親のいずれかが持つ型(= 和集合 S)に含まれる(CraftingStatAlgorithm.md「記法」)。
            var roller = NewRoller();
            var a = StatVector.Of((StatType.AttackPct, 10), (StatType.DefensePct, 1));
            var b = StatVector.Of((StatType.CritRate, 1));
            var union = new[] { StatType.AttackPct, StatType.DefensePct, StatType.CritRate };

            for (int seed = 0; seed < 50; seed++)
            {
                var r = roller.Roll(a, b, new SystemRandomSource(seed));
                foreach (var t in r.Types)
                    Assert.Contains(t, union, $"seed={seed} で親が持たない型 {t}");
                // ウェイト Attack=10 が Defense=1 / CritRate=1 を圧倒するので上位2型に必ず入る。
                // Defense と CritRate は同ウェイトで、どちらが2つ目に入るかは仕様が定めていないため問わない。
                Assert.Contains(
                    StatType.AttackPct,
                    r.Types.ToArray(),
                    $"seed={seed} で最大ウェイトの型が落ちた"
                );
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

        [Test]
        public void Roll_Synergy_PromotesStatSharedByBothParents()
        {
            // 共通型は w_s = (a_s + b_s)(1 + σ) で増幅される(CraftingStatAlgorithm.md 層2)。
            // 素のウェイトでは3番手の CritRate(8)が、σ=0.5 で 12 に増幅され上位2型に食い込む。
            var a = StatVector.Of((StatType.AttackPct, 10), (StatType.CritRate, 4));
            var b = StatVector.Of((StatType.DefensePct, 9), (StatType.CritRate, 4));

            var noSynergy = new CraftingStatRoller(new CraftingParameters { Synergy = 0.0 });
            var withSynergy = new CraftingStatRoller(new CraftingParameters { Synergy = 0.5 });

            int withoutCount = 0;
            int withCount = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                if (
                    noSynergy
                        .Roll(a, b, new SystemRandomSource(seed))
                        .Types.Contains(StatType.CritRate)
                )
                    withoutCount++;
                if (
                    withSynergy
                        .Roll(a, b, new SystemRandomSource(seed))
                        .Types.Contains(StatType.CritRate)
                )
                    withCount++;
            }

            Assert.AreEqual(
                0,
                withoutCount,
                "σ=0 では CritRate(8)が Attack(10)/Defense(9)に負けて候補に入らないはず"
            );
            Assert.Greater(withCount, 0, "σ=0.5 なら CritRate(12)が上位2型に入るはず");
        }

        [Test]
        public void Roll_EpsilonCutoff_DropsStatBelowThreshold()
        {
            // ε 未満の型は捨てて実質1型扱いにする(CraftingStatAlgorithm.md「合成と整理」)。
            var a = StatVector.Of((StatType.AttackPct, 40));
            var b = StatVector.Of((StatType.DefensePct, 10));

            // ε=0(切り捨てなし)を基準にして、この入力・シードでの2型の値を測る。
            var baseline = new CraftingStatRoller(new CraftingParameters { Epsilon = 0.0 }).Roll(
                a,
                b,
                new SystemRandomSource(7)
            );
            Assert.AreEqual(2, baseline.Count, "前提: ε=0 なら2型そろう");
            float smaller = baseline.Types.Min(t => baseline[t]);
            float larger = baseline.Types.Max(t => baseline[t]);
            Assert.Less(smaller, larger, "前提: 2型の値が異なる");

            // ε を小さい方と大きい方の間に置くと、小さい方だけが捨てられる。
            // ε はサンプリング後に適用されるので、同じシードなら値は再現する。
            var r = new CraftingStatRoller(
                new CraftingParameters { Epsilon = (smaller + larger) / 2.0 }
            ).Roll(a, b, new SystemRandomSource(7));

            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(larger, r[r.Types.Single()], 1e-4f);
        }
    }
}
