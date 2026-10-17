using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// TutorialBoss の交戦の時計のうち、追い込み開始時の前倒し（Expedite）の検証。
    /// EditMode では Time.time が進まないため、「今」は Time.time のまま固定として比べる。
    /// </summary>
    public class TBossCombatTempoTests
    {
        private const float CueLead = 0.5f;

        [Test]
        public void 交戦に入っていなければ前倒ししない()
        {
            var tempo = new TBossCombatTempo();

            tempo.Expedite(1f, CueLead);

            Assert.That(tempo.IsScheduled, Is.False);
        }

        [Test]
        public void 次の攻撃が遠ければ追い込みの間隔まで前倒しする()
        {
            var tempo = new TBossCombatTempo();
            tempo.Schedule(100f); // 60〜140秒後
            float now = Time.time;

            tempo.Expedite(1f, CueLead); // 0.6〜1.4秒後に引き直す

            Assert.That(tempo.NextAttackTime, Is.InRange(now + CueLead, now + 1.4f + 0.001f));
        }

        [Test]
        public void 次の攻撃が既に近ければ遅らせない()
        {
            var tempo = new TBossCombatTempo();
            tempo.Schedule(1f); // 0.6〜1.4秒後
            float before = tempo.NextAttackTime;

            tempo.Expedite(100f, CueLead);

            Assert.That(tempo.NextAttackTime, Is.EqualTo(before));
        }

        [Test]
        public void 合図から攻撃までは最低限の間を取る()
        {
            var tempo = new TBossCombatTempo();
            tempo.Schedule(0.001f); // ほぼ今すぐ攻撃する予定
            float now = Time.time;

            tempo.Expedite(0.001f, CueLead);

            Assert.That(tempo.NextAttackTime, Is.EqualTo(now + CueLead).Within(0.0001f));
            Assert.That(tempo.IsAttackDue, Is.False);
        }
    }
}
