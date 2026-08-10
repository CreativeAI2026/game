using CreativeAI.UI.ConversationUI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
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
    }
}
