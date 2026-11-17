using System.Collections.Generic;
using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話テキストの文字位置ごとの待ち時間と揺れ範囲の引き当ての検証。
    /// </summary>
    public class ParsedDialogueTextTests
    {
        private static ParsedDialogueText Create(
            Dictionary<int, float> waits = null,
            List<(int Start, int End)> shakes = null
        ) => new("text", waits ?? new(), shakes ?? new());

        [Test]
        public void GetWaitAfter_ReturnsWaitRegisteredAfterThatCharacter()
        {
            var parsed = Create(new() { [3] = 0.4f });

            Assert.AreEqual(0.4f, parsed.GetWaitAfter(2), 1e-4f);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(-5)]
        public void GetWaitAfter_WithoutRegisteredWait_ReturnsZero(int index)
        {
            var parsed = Create(new() { [3] = 0.4f });

            Assert.AreEqual(0f, parsed.GetWaitAfter(index), 1e-4f);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        public void IsShaking_IncludesStartAndExcludesEnd(int index, bool expected)
        {
            var parsed = Create(shakes: new() { (2, 5) });

            Assert.AreEqual(expected, parsed.IsShaking(index));
        }

        [Test]
        public void IsShaking_WithMultipleRanges_MatchesAnyOfThem()
        {
            var parsed = Create(shakes: new() { (0, 1), (6, 8) });

            Assert.IsTrue(parsed.IsShaking(0));
            Assert.IsFalse(parsed.IsShaking(3));
            Assert.IsTrue(parsed.IsShaking(7));
        }
    }
}
