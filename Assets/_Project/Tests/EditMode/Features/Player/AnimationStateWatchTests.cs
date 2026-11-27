using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// アニメーションのステートに入ったか・抜けたかの見張りの検証。
    /// 入れないままの猶予切れと、入った後に抜けたことを見分けられることを確かめる。
    /// </summary>
    public sealed class AnimationStateWatchTests
    {
        private const float LongTimeout = 1000f;

        [Test]
        public void WaitsUntilEnteredWithinTimeout()
        {
            var watch = new AnimationStateWatch();
            watch.Begin();

            Assert.AreEqual(AnimationStateWatch.Progress.Waiting, watch.Update(false, LongTimeout));
        }

        [Test]
        public void TimesOutWhenNeverEntered()
        {
            var watch = new AnimationStateWatch();
            watch.Begin();

            Assert.AreEqual(AnimationStateWatch.Progress.TimedOut, watch.Update(false, -1f));
        }

        [Test]
        public void ReportsStartedOnlyOnFirstFrameInState()
        {
            var watch = new AnimationStateWatch();
            watch.Begin();

            Assert.AreEqual(AnimationStateWatch.Progress.Started, watch.Update(true, LongTimeout));
            Assert.AreEqual(AnimationStateWatch.Progress.Playing, watch.Update(true, LongTimeout));
        }

        [Test]
        public void ReportsLeftAfterEnteredEvenPastTimeout()
        {
            var watch = new AnimationStateWatch();
            watch.Begin();
            watch.Update(true, -1f);

            Assert.AreEqual(AnimationStateWatch.Progress.Left, watch.Update(false, -1f));
        }

        [Test]
        public void BeginResetsEnteredState()
        {
            var watch = new AnimationStateWatch();
            watch.Begin();
            watch.Update(true, LongTimeout);
            watch.Begin();

            Assert.AreEqual(AnimationStateWatch.Progress.Waiting, watch.Update(false, LongTimeout));
        }
    }
}
