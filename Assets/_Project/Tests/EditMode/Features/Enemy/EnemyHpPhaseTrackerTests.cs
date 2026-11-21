using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 敵のHP割合から段階を決める処理（EnemyHpPhaseTracker.ResolvePhaseIndex）の検証。
    /// </summary>
    public class EnemyHpPhaseTrackerTests
    {
        private static readonly float[] PhaseHpRatios = { 1f, 0.66f, 0.33f };

        [Test]
        public void HPが最初の境目より上なら段階は進まない()
        {
            Assert.That(
                EnemyHpPhaseTracker.ResolvePhaseIndex(0, 0.8f, PhaseHpRatios),
                Is.EqualTo(0)
            );
        }

        [Test]
        public void 境目ちょうどで次の段階に入る()
        {
            Assert.That(
                EnemyHpPhaseTracker.ResolvePhaseIndex(0, 0.66f, PhaseHpRatios),
                Is.EqualTo(1)
            );
        }

        [Test]
        public void 大ダメージで複数の段階をまたげば最後の段階まで進む()
        {
            Assert.That(
                EnemyHpPhaseTracker.ResolvePhaseIndex(0, 0.1f, PhaseHpRatios),
                Is.EqualTo(2)
            );
        }

        [Test]
        public void 回復しても段階は戻らない()
        {
            Assert.That(EnemyHpPhaseTracker.ResolvePhaseIndex(2, 1f, PhaseHpRatios), Is.EqualTo(2));
        }

        [Test]
        public void HPが0でも最後の段階を越えない()
        {
            Assert.That(EnemyHpPhaseTracker.ResolvePhaseIndex(1, 0f, PhaseHpRatios), Is.EqualTo(2));
        }
    }
}
