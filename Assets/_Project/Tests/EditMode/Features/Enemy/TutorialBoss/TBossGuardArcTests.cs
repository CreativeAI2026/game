using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// TBoss の防御が正面からの攻撃だけを受け止めるか（TBossGuard.IsInGuardArc）の検証。
    /// </summary>
    public class TBossGuardArcTests
    {
        private const float HalfAngle = 90f;

        private static bool IsBlocked(Vector3 hitPoint) =>
            TBossGuard.IsInGuardArc(Vector3.zero, Vector3.forward, hitPoint, HalfAngle);

        [Test]
        public void 正面からの攻撃は受け止める()
        {
            Assert.That(IsBlocked(new Vector3(0f, 1f, 2f)), Is.True);
        }

        [Test]
        public void 斜め前からの攻撃は受け止める()
        {
            Assert.That(IsBlocked(new Vector3(2f, 0f, 0.5f)), Is.True);
        }

        [Test]
        public void 背後からの攻撃は通る()
        {
            Assert.That(IsBlocked(new Vector3(0f, 1f, -2f)), Is.False);
        }

        [Test]
        public void 斜め後ろからの攻撃は通る()
        {
            Assert.That(IsBlocked(new Vector3(2f, 0f, -0.5f)), Is.False);
        }

        [Test]
        public void 高さの違いは向きの判定に使わない()
        {
            // 真上寄りでも、水平方向で正面なら受け止める（矢を上から撃ち下ろされた場合など）
            Assert.That(IsBlocked(new Vector3(0f, 10f, 0.1f)), Is.True);
        }

        [Test]
        public void 体の中心に当たった時は受け止める()
        {
            Assert.That(IsBlocked(new Vector3(0f, 1f, 0f)), Is.True);
        }

        [Test]
        public void 角度を狭めると横からの攻撃は通る()
        {
            bool blocked = TBossGuard.IsInGuardArc(
                Vector3.zero,
                Vector3.forward,
                new Vector3(2f, 0f, 1f),
                45f
            );

            Assert.That(blocked, Is.False);
        }
    }
}
