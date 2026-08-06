using System.Collections.Generic;
using System.Linq;
using CreativeAI.Crafting;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ドロップ装備品の付与型抽選(documents/Specification.md §2.1.1)の検証。
    /// 重み 攻撃%2 / 防御%2 / 最大HP%2 / 会心ダメージ1 / 会心率1 の非復元抽出。
    /// </summary>
    public class DropStatTypeRollerTests
    {
        [Test]
        public void Weights_MatchSpecTable()
        {
            var w = DropStatTypeRoller.Weights.ToDictionary(x => x.Type, x => x.Weight);

            Assert.AreEqual(2, w[StatType.AttackPct]);
            Assert.AreEqual(2, w[StatType.DefensePct]);
            Assert.AreEqual(2, w[StatType.MaxHpPct]);
            Assert.AreEqual(1, w[StatType.CritDamage]);
            Assert.AreEqual(1, w[StatType.CritRate]);
            Assert.AreEqual(5, w.Count, "装備品の付与候補は5型のみ(食材の HealAmount は含めない)");
            Assert.IsFalse(w.ContainsKey(StatType.HealAmount));
        }

        [Test]
        public void Roll_ReturnsAtMostTwoTypes_WithoutDuplicates()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var picked = DropStatTypeRoller.Roll(new SystemRandomSource(seed));

                Assert.LessOrEqual(picked.Count, DropStatTypeRoller.MaxStatCount, $"seed={seed}");
                Assert.AreEqual(
                    picked.Count,
                    picked.Distinct().Count(),
                    $"seed={seed} で同じ型が重複した(非復元抽出のはず)"
                );
            }
        }

        [Test]
        public void Roll_NonReplacement_SecondDrawExcludesFirstPick()
        {
            // uniform=0 は常に「残っている候補の先頭」を引く。復元抽出なら 2 回とも攻撃% になるが、
            // 非復元なので 2 回目は候補から外れて次の型(防御%)になる。
            var picked = DropStatTypeRoller.Roll(
                new SequenceRandomSource(new[] { 0.0, 0.0 }),
                count: 2
            );

            CollectionAssert.AreEqual(new[] { StatType.AttackPct, StatType.DefensePct }, picked);
        }

        [Test]
        public void Roll_IsDeterministic_ForSameSeed()
        {
            var r1 = DropStatTypeRoller.Roll(new SystemRandomSource(42));
            var r2 = DropStatTypeRoller.Roll(new SystemRandomSource(42));

            CollectionAssert.AreEqual(r1, r2);
        }

        [Test]
        public void Roll_CountIsClampedToZeroAndMax()
        {
            Assert.AreEqual(0, DropStatTypeRoller.Roll(new SystemRandomSource(0), 0).Count);
            Assert.AreEqual(0, DropStatTypeRoller.Roll(new SystemRandomSource(0), -3).Count);
            Assert.AreEqual(
                DropStatTypeRoller.MaxStatCount,
                DropStatTypeRoller.Roll(new SystemRandomSource(0), 99).Count,
                "付与数は最大2つ(Specification.md §2.1)"
            );
        }

        [Test]
        public void Roll_SingleDraw_DistributionMatchesWeights()
        {
            // 1型だけ引いたときの出現率が 25/25/25/12.5/12.5% に収束することを確認する。
            const int samples = 100_000;
            var counts = new Dictionary<StatType, int>();
            var rng = new SystemRandomSource(12345);

            for (int i = 0; i < samples; i++)
            {
                var t = DropStatTypeRoller.Roll(rng, count: 1).Single();
                counts.TryGetValue(t, out int c);
                counts[t] = c + 1;
            }

            AssertRate(counts, StatType.AttackPct, 0.250, samples);
            AssertRate(counts, StatType.DefensePct, 0.250, samples);
            AssertRate(counts, StatType.MaxHpPct, 0.250, samples);
            AssertRate(counts, StatType.CritDamage, 0.125, samples);
            AssertRate(counts, StatType.CritRate, 0.125, samples);
        }

        private static void AssertRate(
            IReadOnlyDictionary<StatType, int> counts,
            StatType type,
            double expected,
            int samples
        )
        {
            counts.TryGetValue(type, out int c);
            double actual = (double)c / samples;
            Assert.AreEqual(
                expected,
                actual,
                0.01,
                $"{type} の出現率が重み({expected:P1})から外れた: {actual:P2}"
            );
        }
    }
}
