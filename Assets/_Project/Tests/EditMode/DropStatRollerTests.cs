using System;
using System.Linq;
using CreativeAI.Crafting;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ドロップ装備品の付与ステータス(型 + 量)のロール
    /// (documents/Specification.md §2.1.1 / CraftingStatAlgorithm.md「ドロップ(拾得)のロール」)。
    /// 型は DropStatTypeRoller、量は「総パワー(= シードの固定値合計)× ディリクレ均等配分」。
    /// </summary>
    public class DropStatRollerTests
    {
        private static readonly StatType[] EquipmentStatTypes =
        {
            StatType.AttackPct,
            StatType.DefensePct,
            StatType.MaxHpPct,
            StatType.CritDamage,
            StatType.CritRate,
        };

        [Test]
        public void Roll_NullRandom_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DropStatRoller.Roll(20.0, null));
        }

        [Test]
        public void Roll_ZeroOrNegativePower_IsEmpty()
        {
            Assert.AreEqual(0, DropStatRoller.Roll(0.0, new SystemRandomSource(1)).Count);
            Assert.AreEqual(0, DropStatRoller.Roll(-5.0, new SystemRandomSource(1)).Count);
        }

        [Test]
        public void Roll_YieldsAtMostTwoEquipmentTypes()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var rolled = DropStatRoller.Roll(20.0, new SystemRandomSource(seed));

                Assert.LessOrEqual(rolled.Count, 2, $"seed={seed}: 付与数は最大2つ(§2.1)");
                Assert.Greater(rolled.Count, 0, $"seed={seed}: 総パワーがあるので1つは付く");
                CollectionAssert.IsSubsetOf(
                    rolled.Types.ToArray(),
                    EquipmentStatTypes,
                    $"seed={seed}: 装備品の候補5型以外が出た(食材の HealAmount は対象外)"
                );
            }
        }

        [Test]
        public void Roll_TotalNeverExceedsSeedPower()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var rolled = DropStatRoller.Roll(20.0, new SystemRandomSource(seed));

                Assert.LessOrEqual(
                    rolled.Power,
                    20.0f + 1e-3f,
                    $"seed={seed}: 配分の合計が総パワーを超えた"
                );
            }
        }

        [Test]
        public void Roll_TotalIsCappedByPowerCap()
        {
            // シードの宣言値が C_cap(既定100)を超えても、そこで打ち止め(連鎖インフレ防止)。
            var rolled = DropStatRoller.Roll(500.0, new SystemRandomSource(7));

            Assert.LessOrEqual(rolled.Power, (float)CraftingParameters.Default.PowerCap + 1e-3f);
        }

        [Test]
        public void Roll_SingleStat_GetsTheWholeBudget()
        {
            var rolled = DropStatRoller.Roll(20.0, new SystemRandomSource(3), statCount: 1);

            Assert.AreEqual(1, rolled.Count);
            Assert.AreEqual(20.0f, rolled.Power, 1e-3f, "1型なら総パワーを丸ごと乗せる");
        }

        [Test]
        public void Roll_ZeroStatCount_IsEmpty()
        {
            Assert.AreEqual(
                0,
                DropStatRoller.Roll(20.0, new SystemRandomSource(3), statCount: 0).Count
            );
        }

        [Test]
        public void Roll_SameSeed_IsDeterministic()
        {
            var a = DropStatRoller.Roll(20.0, new SystemRandomSource(42));
            var b = DropStatRoller.Roll(20.0, new SystemRandomSource(42));

            CollectionAssert.AreEquivalent(a.Types.ToArray(), b.Types.ToArray());
            foreach (var type in a.Types)
                Assert.AreEqual(a[type], b[type], 1e-6f);
        }

        [Test]
        public void Roll_ProducesIndividualVariation()
        {
            // 「同じ装備品でも拾うたびに違う個体になる」(§2.1.1)。型か量のどちらかが必ず散る。
            var first = DropStatRoller.Roll(20.0, new SystemRandomSource(1));
            bool sawDifference = false;

            for (int seed = 2; seed < 30 && !sawDifference; seed++)
            {
                var other = DropStatRoller.Roll(20.0, new SystemRandomSource(seed));
                sawDifference =
                    !first.Types.OrderBy(t => t).SequenceEqual(other.Types.OrderBy(t => t))
                    || first.Types.Any(t => Math.Abs(first[t] - other[t]) > 1e-3f);
            }

            Assert.IsTrue(sawDifference, "何度ロールしても同じ結果しか出ない(個体差が無い)");
        }

        [Test]
        public void Roll_CritRateIsClampedToItsCap()
        {
            // 会心率は 100% 上限(CraftingParameters.Caps)。総パワー上限と同値なので超えない。
            for (int seed = 0; seed < 100; seed++)
            {
                var rolled = DropStatRoller.Roll(500.0, new SystemRandomSource(seed), statCount: 1);
                Assert.LessOrEqual(rolled[StatType.CritRate], 100f + 1e-3f, $"seed={seed}");
            }
        }
    }
}
