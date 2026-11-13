using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 剣の防御メーター(受けると減り、時間で回復し、尽きると割れる)の検証。
    /// 防御を小刻みに解いても受けられる量が増えないことを、ここで保証する。
    /// </summary>
    public class GuardGaugeTests
    {
        private const float Max = 1000f;
        private const float RegenPerSecond = 100f;
        private const float RegenDelay = 2f;
        private const float RecoverRatio = 0.5f;

        private static GuardGauge CreateGauge(float costPerDamage = 1f)
        {
            return new GuardGauge(
                new GuardGaugeSettings(Max, costPerDamage, RegenPerSecond, RegenDelay, RecoverRatio)
            );
        }

        [Test]
        public void StartsFull()
        {
            GuardGauge gauge = CreateGauge();

            Assert.AreEqual(Max, gauge.GetCurrent(0f), 1e-3f);
            Assert.IsFalse(gauge.IsBroken(0f));
        }

        [Test]
        public void Absorb_ReducesByDamageTimesCost()
        {
            GuardGauge gauge = CreateGauge(costPerDamage: 0.5f);
            gauge.BeginGuard(0f);

            bool broke = gauge.Absorb(400f, 0f);

            Assert.IsFalse(broke);
            Assert.AreEqual(800f, gauge.GetCurrent(0f), 1e-3f);
        }

        [Test]
        public void HeavierAttack_BreaksInFewerHits()
        {
            Assert.Less(CountHitsToBreak(500f), CountHitsToBreak(250f));
        }

        [Test]
        public void ReleasingGuardBetweenHits_DoesNotRestoreGauge()
        {
            GuardGauge gauge = CreateGauge();

            // 回復待ちの時間より短い間隔で、1発ごとに防御を解いて構え直す
            float now = 0f;
            int hits = 0;
            bool broke = false;
            while (!broke)
            {
                gauge.BeginGuard(now);
                broke = gauge.Absorb(250f, now);
                gauge.EndGuard(now);
                hits++;
                now += RegenDelay * 0.5f;
            }

            Assert.AreEqual(CountHitsToBreak(250f), hits);
        }

        [Test]
        public void DoesNotRegen_WhileGuarding()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);
            gauge.Absorb(400f, 0f);

            Assert.AreEqual(600f, gauge.GetCurrent(100f), 1e-3f);
        }

        [Test]
        public void DoesNotRegen_UntilDelayPasses()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);
            gauge.Absorb(400f, 0f);
            gauge.EndGuard(0f);

            Assert.AreEqual(600f, gauge.GetCurrent(RegenDelay), 1e-3f);
        }

        [Test]
        public void Regens_AfterDelay_UpToMax()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);
            gauge.Absorb(400f, 0f);
            gauge.EndGuard(0f);

            Assert.AreEqual(700f, gauge.GetCurrent(RegenDelay + 1f), 1e-3f);
            Assert.AreEqual(Max, gauge.GetCurrent(RegenDelay + 100f), 1e-3f);
        }

        [Test]
        public void GuardHeldDuringDelay_RegensOnlyAfterRelease()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);
            gauge.Absorb(400f, 0f);
            // 回復待ちが明けた後も構え続け、10 秒の時点で解く
            gauge.EndGuard(10f);

            Assert.AreEqual(600f, gauge.GetCurrent(10f), 1e-3f);
            Assert.AreEqual(700f, gauge.GetCurrent(11f), 1e-3f);
        }

        [Test]
        public void Absorb_BreaksWhenGaugeRunsOut()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);

            bool broke = gauge.Absorb(Max + 1f, 0f);

            Assert.IsTrue(broke);
            Assert.AreEqual(0f, gauge.GetCurrent(0f), 1e-3f);
            Assert.IsTrue(gauge.IsBroken(0f));
        }

        [Test]
        public void StaysBroken_UntilRecoverRatio()
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);
            gauge.Absorb(Max, 0f);
            gauge.EndGuard(0f);

            // 回復待ち 2 秒の後、毎秒 100 回復。半分(500)に届くのは 7 秒の時点
            Assert.IsTrue(gauge.IsBroken(6.9f));
            Assert.IsFalse(gauge.IsBroken(7.1f));
        }

        private static int CountHitsToBreak(float damage)
        {
            GuardGauge gauge = CreateGauge();
            gauge.BeginGuard(0f);

            int hits = 1;
            while (!gauge.Absorb(damage, 0f))
                hits++;
            return hits;
        }
    }
}
