using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話中の既読行と選択済み選択肢の記録(行の同一判定・既読の消去)の検証。
    /// </summary>
    public class DialogueSessionStateTests
    {
        [Test]
        public void BeginLine_FirstTime_IsNotRead_AndAfterMarkRead_IsRead()
        {
            var state = new DialogueSessionState();

            state.BeginLine("主人公", "hero_normal", "こんにちは");
            Assert.IsFalse(state.CurrentLineWasRead);

            state.MarkCurrentLineRead();
            state.BeginLine("主人公", "hero_normal", "こんにちは");
            Assert.IsTrue(state.CurrentLineWasRead);
        }

        [TestCase("敵", "hero_normal", "こんにちは")]
        [TestCase("主人公", "hero_smile", "こんにちは")]
        [TestCase("主人公", "hero_normal", "さようなら")]
        public void LineIdentity_DiffersWhenSpeakerPortraitOrTextDiffers(
            string speaker,
            string portrait,
            string text
        )
        {
            var state = new DialogueSessionState();
            state.MarkLineRead("主人公", "hero_normal", "こんにちは");

            Assert.IsFalse(state.IsLineRead(speaker, portrait, text));
            Assert.IsTrue(state.IsLineRead("主人公", "hero_normal", "こんにちは"));
        }

        [Test]
        public void MarkCurrentLineRead_BeforeAnyLine_DoesNothing()
        {
            var state = new DialogueSessionState();

            Assert.DoesNotThrow(state.MarkCurrentLineRead);
            Assert.IsNull(state.CurrentLineId);
        }

        [Test]
        public void BeginReward_IsTrackedAsRewardSpeakerLine()
        {
            var state = new DialogueSessionState();

            state.BeginReward("薬草を手に入れた");
            state.MarkCurrentLineRead();

            Assert.IsTrue(state.IsLineRead("reward", string.Empty, "薬草を手に入れた"));
        }

        [Test]
        public void ClearReadHistory_ForgetsAllLines_ButKeepsSelectedChoices()
        {
            var state = new DialogueSessionState();
            state.MarkLineRead("a", "b", "c");
            state.MarkChoiceSelected("yes");

            state.ClearReadHistory();

            Assert.IsFalse(state.IsLineRead("a", "b", "c"));
            CollectionAssert.Contains(state.SelectedChoiceValues, "yes");
        }

        [Test]
        public void MarkChoiceSelected_IgnoresEmptyAndDuplicates()
        {
            var state = new DialogueSessionState();

            state.MarkChoiceSelected(null);
            state.MarkChoiceSelected(string.Empty);
            state.MarkChoiceSelected("yes");
            state.MarkChoiceSelected("yes");

            Assert.AreEqual(1, state.SelectedChoiceValues.Count);
        }
    }
}
