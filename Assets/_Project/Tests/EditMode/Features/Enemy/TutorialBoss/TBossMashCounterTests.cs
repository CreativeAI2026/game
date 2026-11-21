using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// TBoss のはじき返しのきっかけになる、連打の数え方（一定時間内の被弾回数）の検証。
    /// </summary>
    public class TBossMashCounterTests
    {
        private const float Window = 2f;
        private const int HitCount = 4;

        [Test]
        public void 時間内に回数に届かなければ連打ではない()
        {
            var counter = new TBossMashCounter();
            counter.Register(0f);
            counter.Register(0.5f);
            counter.Register(1f);

            Assert.That(counter.IsMashed(1f, Window, HitCount), Is.False);
        }

        [Test]
        public void 時間内に回数に届けば連打とみなす()
        {
            var counter = new TBossMashCounter();
            counter.Register(0f);
            counter.Register(0.5f);
            counter.Register(1f);
            counter.Register(1.5f);

            Assert.That(counter.IsMashed(1.5f, Window, HitCount), Is.True);
        }

        [Test]
        public void 時間の幅より古い被弾は数えない()
        {
            var counter = new TBossMashCounter();
            counter.Register(0f);
            counter.Register(1f);
            counter.Register(2f);
            counter.Register(2.5f);

            // 3秒の時点では0秒の被弾が幅（2秒）の外になり、3回しか残らない
            Assert.That(counter.IsMashed(3f, Window, HitCount), Is.False);
        }

        [Test]
        public void ちょうど時間の幅の端の被弾は数える()
        {
            var counter = new TBossMashCounter();
            counter.Register(0f);
            counter.Register(1f);
            counter.Register(1.5f);
            counter.Register(2f);

            Assert.That(counter.IsMashed(2f, Window, HitCount), Is.True);
        }

        [Test]
        public void 記録を消すと連打ではなくなる()
        {
            var counter = new TBossMashCounter();
            for (int i = 0; i < HitCount; i++)
            {
                counter.Register(i * 0.1f);
            }

            counter.Clear();

            Assert.That(counter.IsMashed(0.5f, Window, HitCount), Is.False);
        }
    }
}
