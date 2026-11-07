using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話テキストのタグ解析(制御タグ除去・TMP タグ温存・待ち時間のクランプ)の検証。
    /// </summary>
    public class DialogueMarkupParserTests
    {
        [Test]
        public void Parse_RemovesControlTagsAndPreservesTmpTags()
        {
            var parsed = DialogueMarkupParser.Parse(
                "危険<wait=0.5><shake><color=#ff0000>です</color></shake>"
            );

            Assert.AreEqual("危険<color=#ff0000>です</color>", parsed.Text);
            Assert.AreEqual(0.5f, parsed.GetWaitAfter(1), 0.001f);
            Assert.IsTrue(parsed.IsShaking(2));
            Assert.IsFalse(parsed.IsShaking(1));
        }

        [Test]
        public void Parse_ClampsWaitAndToleratesMalformedInput()
        {
            var parsed = DialogueMarkupParser.Parse("A<wait=99>B<wait=nope>C");

            Assert.AreEqual("ABC", parsed.Text);
            Assert.AreEqual(10f, parsed.GetWaitAfter(0), 0.001f);
        }

        [Test]
        public void Parse_ConvertsEmotionTagsToTmpFormatting()
        {
            var parsed = DialogueMarkupParser.Parse(
                "<whisper>小声</whisper><shout>叫ぶ</shout><emphasis>重要</emphasis>"
            );

            StringAssert.Contains("<size=85%><color=#B7C1D0>小声</color></size>", parsed.Text);
            StringAssert.Contains("<size=115%><b>叫ぶ</b></size>", parsed.Text);
            StringAssert.Contains("<color=#86D7FF><b>重要</b></color>", parsed.Text);
        }

        [Test]
        public void Parse_SpeedTag_ChangesScaleFromThatCharacterAndResets()
        {
            var parsed = DialogueMarkupParser.Parse("A<speed=2>BC</speed>D");

            Assert.AreEqual("ABCD", parsed.Text);
            Assert.IsFalse(parsed.TryGetSpeedChange(0, out _));
            Assert.IsTrue(parsed.TryGetSpeedChange(1, out float fast));
            Assert.AreEqual(2f, fast, 0.001f);
            Assert.IsTrue(parsed.TryGetSpeedChange(3, out float normal));
            Assert.AreEqual(1f, normal, 0.001f);
        }

        [Test]
        public void Parse_AutoTag_SetsAdvanceSecondsAndIsRemovedFromText()
        {
            var parsed = DialogueMarkupParser.Parse("ガァ……<auto=1.5>");

            Assert.AreEqual("ガァ……", parsed.Text);
            Assert.IsTrue(parsed.HasAutoAdvance);
            Assert.AreEqual(1.5f, parsed.AutoAdvanceSeconds, 0.001f);
            Assert.IsFalse(DialogueMarkupParser.Parse("通常").HasAutoAdvance);
        }

        [Test]
        public void Parse_SameSourceTwice_ReusesTheResult_AndDifferentSourceDoesNot()
        {
            var first = DialogueMarkupParser.Parse("同じ<wait=0.1>本文");
            var again = DialogueMarkupParser.Parse(new string("同じ<wait=0.1>本文".ToCharArray()));
            var other = DialogueMarkupParser.Parse("別の本文");

            Assert.AreSame(first, again);
            Assert.AreNotSame(first, other);
            Assert.AreEqual("別の本文", other.Text);
        }
    }
}
