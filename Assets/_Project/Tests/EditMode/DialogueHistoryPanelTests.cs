using CreativeAI.UI.ConversationUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class DialogueHistoryPanelTests
    {
        [Test]
        public void AddEntry_StacksHistoryAndCanOpenAndClose()
        {
            var root = new GameObject("HistoryRoot", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<DialogueHistoryPanel>();
                panel.Initialize(null);

                panel.AddEntry("主人公", "最初の発言", null, DialoguePortraitSide.Left);
                panel.AddEntry("相手", "次の発言", null, DialoguePortraitSide.Right);
                panel.AddChoiceEntry("一緒に行く");

                Assert.AreEqual(3, panel.EntryCount);
                panel.SetOpen(true);
                Assert.IsTrue(panel.IsOpen);
                panel.SetOpen(false);
                Assert.IsFalse(panel.IsOpen);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
