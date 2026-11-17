using CreativeAI.Core;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// events.json の useItem ステップ(アイテムを使う調べ方)の検証。
    /// </summary>
    public class EventUseItemTests
    {
        [Test]
        public void UseItem_Factory_ExposesFields()
        {
            var step = EventStep.UseItem(
                new[] { new UseItemCandidate("card_key", 2) },
                true,
                "違うようだ。",
                "係員",
                "p1",
                "何を使う?",
                true
            );

            Assert.AreEqual(StepKind.UseItem, step.Kind);
            Assert.AreEqual("card_key", step.Accept[0].ItemKey);
            Assert.AreEqual(2, step.Accept[0].Count);
            Assert.IsTrue(step.Consume);
            Assert.AreEqual("違うようだ。", step.Text);
            Assert.AreEqual("係員", step.Speaker);
            Assert.AreEqual("p1", step.Portrait);
            Assert.AreEqual("何を使う?", step.Prompt);
            Assert.IsTrue(step.EndOnWrong);
        }
    }
}
