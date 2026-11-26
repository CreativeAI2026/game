using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話履歴パネルの追加・上限を超えた分のトリム・絞り込みの検証。
    /// </summary>
    public class DialogueHistoryPanelTests
    {
        private const string ViewPath =
            "Assets/_Project/Features/UI/Conversation/Prefabs/ConversationView.prefab";

        private GameObject _root;
        private DialogueHistoryPanel _panel;
        private RectTransform _content;

        [SetUp]
        public void SetUp()
        {
            _root = PrefabUtility.LoadPrefabContents(ViewPath);
            _panel = _root.GetComponent<DialogueHistoryPanel>();
            _content = TestReflection.GetField<RectTransform>(_panel, "_content");
            _panel.Initialize();
        }

        [TearDown]
        public void TearDown() => PrefabUtility.UnloadPrefabContents(_root);

        [Test]
        public void AddEntry_StacksHistoryAndCanOpenAndClose()
        {
            _panel.AddEntry("主人公", "最初の発言", null, DialoguePortraitSide.Left);
            _panel.AddEntry("相手", "次の発言", null, DialoguePortraitSide.Right);
            _panel.AddChoiceEntry("一緒に行く");
            _panel.AddRewardEntry("鍵を手に入れた。", false);

            Assert.AreEqual(4, _panel.EntryCount);
            Assert.AreEqual(4, _content.childCount);
            _panel.SetOpen(true);
            Assert.IsTrue(_panel.IsOpen);
            _panel.SetOpen(false);
            Assert.IsFalse(_panel.IsOpen);
        }

        [Test]
        public void AddEntry_OverMaximum_TrimsOldestEntryAndView()
        {
            TestReflection.SetField(_panel, "_maxEntries", 2);
            _panel.AddEntry("A", "one", null, DialoguePortraitSide.Left);
            _panel.AddEntry("B", "two", null, DialoguePortraitSide.Right);
            _panel.AddEntry("C", "three", null, DialoguePortraitSide.Left);

            Assert.AreEqual(2, _panel.EntryCount);
            Assert.AreEqual(2, _content.childCount);
        }

        [Test]
        public void ApplyFilter_MatchesSpeakerOrBody()
        {
            _panel.AddEntry("主人公", "森へ行く", null, DialoguePortraitSide.Left);
            _panel.AddEntry("ロボ", "待機します", null, DialoguePortraitSide.Right);

            _panel.ApplyFilter("森");

            Assert.IsTrue(_content.GetChild(0).gameObject.activeSelf);
            Assert.IsFalse(_content.GetChild(1).gameObject.activeSelf);
        }
    }
}
