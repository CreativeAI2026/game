using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 入手した装備品の付与ステータスの抽選(DropStatRoller)の検証。
    /// 付与数は 1〜2、型は重み 攻撃%2 / 防御%2 / 最大HP%2 / 会心ダメージ1 / 会心率1 の非復元抽出、値は型ごとの4段階から等確率。
    /// </summary>
    public class DropStatRollerTests
    {
        [Test]
        public void Roll_NullRandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DropStatRoller.RollDropStats(null));
        }

        [Test]
        public void Roll_YieldsOneOrTwoEquipmentTypes_WithTierValues()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var rolled = DropStatRoller.RollDropStats(new SystemRandomSource(seed));

                Assert.That(rolled.Count, Is.InRange(1, 2), $"seed={seed}: 付与数は1〜2つ");
                foreach (var type in rolled.Types)
                {
                    Assert.IsTrue(
                        DropStatRoller.DropStatTiers.ContainsKey(type),
                        $"seed={seed}: 装備品の候補5型以外が出た(食材の HealAmount は対象外)"
                    );
                    CollectionAssert.Contains(
                        DropStatRoller.DropStatTiers[type].ToList(),
                        rolled[type],
                        $"seed={seed}: {type} の値が4段階のどれでもない"
                    );
                }
            }
        }

        [Test]
        public void Roll_BothOneAndTwoStatsAppear()
        {
            var counts = Enumerable
                .Range(0, 100)
                .Select(seed => DropStatRoller.RollDropStats(new SystemRandomSource(seed)).Count)
                .ToList();

            CollectionAssert.Contains(counts, 1);
            CollectionAssert.Contains(counts, 2);
        }

        [Test]
        public void Roll_EveryTierAppears()
        {
            // 4段階すべてが選ばれうること(端の段階が出ない丸め誤りを防ぐ)。
            var seen = Enumerable
                .Range(0, 400)
                .Select(seed =>
                    DropStatRoller.RollDropStats(new SystemRandomSource(seed), statCount: 1)
                )
                .Where(rolled => rolled.Types.Contains(StatType.AttackPct))
                .Select(rolled => rolled[StatType.AttackPct])
                .Distinct()
                .ToList();

            CollectionAssert.AreEquivalent(
                DropStatRoller.DropStatTiers[StatType.AttackPct].ToList(),
                seen
            );
        }

        [Test]
        public void Roll_ZeroStatCount_IsEmpty()
        {
            Assert.AreEqual(
                0,
                DropStatRoller.RollDropStats(new SystemRandomSource(3), statCount: 0).Count
            );
        }

        [Test]
        public void Roll_SameSeed_IsDeterministic()
        {
            var a = DropStatRoller.RollDropStats(new SystemRandomSource(42));
            var b = DropStatRoller.RollDropStats(new SystemRandomSource(42));

            CollectionAssert.AreEquivalent(a.Types.ToArray(), b.Types.ToArray());
            foreach (var type in a.Types)
                Assert.AreEqual(a[type], b[type], 1e-6f);
        }

        [Test]
        public void Roll_ProducesIndividualVariation()
        {
            // 「同じ装備品でも拾うたびに違う個体になる」。型か量のどちらかが必ず散る。
            var first = DropStatRoller.RollDropStats(new SystemRandomSource(1));
            bool sawDifference = false;

            for (int seed = 2; seed < 30 && !sawDifference; seed++)
            {
                var other = DropStatRoller.RollDropStats(new SystemRandomSource(seed));
                sawDifference =
                    !first.Types.OrderBy(t => t).SequenceEqual(other.Types.OrderBy(t => t))
                    || first.Types.Any(t => Math.Abs(first[t] - other[t]) > 1e-3f);
            }

            Assert.IsTrue(sawDifference, "何度ロールしても同じ結果しか出ない(個体差が無い)");
        }

        // --- 型の抽選 ---

        [Test]
        public void Weights_MatchSpecTable()
        {
            var w = DropStatRoller.DropStatWeights.ToDictionary(x => x.Type, x => x.Weight);

            Assert.AreEqual(2, w[StatType.AttackPct]);
            Assert.AreEqual(2, w[StatType.DefensePct]);
            Assert.AreEqual(2, w[StatType.MaxHpPct]);
            Assert.AreEqual(1, w[StatType.CritDamage]);
            Assert.AreEqual(1, w[StatType.CritRate]);
            Assert.AreEqual(5, w.Count, "装備品の付与候補は5型のみ(食材の HealAmount は含めない)");
            Assert.IsFalse(w.ContainsKey(StatType.HealAmount));
        }

        [Test]
        public void RollTypes_ReturnsAtMostTwoTypes_WithoutDuplicates()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var picked = DropStatRoller.RollDropStatTypes(new SystemRandomSource(seed));

                Assert.LessOrEqual(picked.Count, DropStatRoller.MaxDropStatCount, $"seed={seed}");
                Assert.AreEqual(
                    picked.Count,
                    picked.Distinct().Count(),
                    $"seed={seed} で同じ型が重複した(非復元抽出のはず)"
                );
            }
        }

        [Test]
        public void RollTypes_NonReplacement_SecondDrawExcludesFirstPick()
        {
            // uniform=0 は常に「残っている候補の先頭」を引く。復元抽出なら 2 回とも攻撃% になるが、
            // 非復元なので 2 回目は候補から外れて次の型(防御%)になる。
            var picked = DropStatRoller.RollDropStatTypes(
                new SequenceRandomSource(new[] { 0.0, 0.0 }),
                count: 2
            );

            CollectionAssert.AreEqual(new[] { StatType.AttackPct, StatType.DefensePct }, picked);
        }

        [Test]
        public void RollTypes_IsDeterministic_ForSameSeed()
        {
            var r1 = DropStatRoller.RollDropStatTypes(new SystemRandomSource(42));
            var r2 = DropStatRoller.RollDropStatTypes(new SystemRandomSource(42));

            CollectionAssert.AreEqual(r1, r2);
        }

        [Test]
        public void RollTypes_CountIsClampedToZeroAndMax()
        {
            Assert.AreEqual(
                0,
                DropStatRoller.RollDropStatTypes(new SystemRandomSource(0), 0).Count
            );
            Assert.AreEqual(
                0,
                DropStatRoller.RollDropStatTypes(new SystemRandomSource(0), -3).Count
            );
            Assert.AreEqual(
                DropStatRoller.MaxDropStatCount,
                DropStatRoller.RollDropStatTypes(new SystemRandomSource(0), 99).Count,
                "付与数は最大2つ"
            );
        }

        [Test]
        public void RollTypes_SingleDraw_DistributionMatchesWeights()
        {
            // 1型だけ引いたときの出現率が 25/25/25/12.5/12.5% に収束することを確認する。
            const int samples = 100_000;
            var counts = new Dictionary<StatType, int>();
            var rng = new SystemRandomSource(12345);

            for (int i = 0; i < samples; i++)
            {
                var t = DropStatRoller.RollDropStatTypes(rng, count: 1).Single();
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
