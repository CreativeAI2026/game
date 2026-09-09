using CreativeAI.UI.ConversationUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話履歴パネルの追加・上限を超えた分のトリム・絞り込みの検証。
    /// </summary>
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

        [Test]
        public void AddEntry_OverMaximum_TrimsOldestEntryAndView()
        {
            var root = new GameObject("HistoryRoot", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<DialogueHistoryPanel>();
                TestReflection.SetField(panel, "_maxEntries", 2);
                panel.Initialize(null);
                panel.AddEntry("A", "one", null, DialoguePortraitSide.Left);
                panel.AddEntry("B", "two", null, DialoguePortraitSide.Right);
                panel.AddEntry("C", "three", null, DialoguePortraitSide.Left);

                Assert.AreEqual(2, panel.EntryCount);
                Assert.AreEqual(
                    2,
                    TestReflection.GetField<RectTransform>(panel, "_content").childCount
                );
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyFilter_MatchesSpeakerOrBody()
        {
            var root = new GameObject("HistoryRoot", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<DialogueHistoryPanel>();
                panel.Initialize(null);
                panel.AddEntry("主人公", "森へ行く", null, DialoguePortraitSide.Left);
                panel.AddEntry("ロボ", "待機します", null, DialoguePortraitSide.Right);
                var content = TestReflection.GetField<RectTransform>(panel, "_content");

                panel.ApplyFilter("森");

                Assert.IsTrue(content.GetChild(0).gameObject.activeSelf);
                Assert.IsFalse(content.GetChild(1).gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
