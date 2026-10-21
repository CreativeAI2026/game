using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.EditorTools;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class EventBranchTests
    {
        private static string Document(string steps) =>
            "{\"events\":[{\"id\":\"branch_test\",\"conditions\":[{\"type\":\"progress\",\"value\":0}],\"nextProgress\":1,\"steps\":["
            + steps
            + "]}]}";

        private static void Drive(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var current = stack.Peek();
                if (!current.MoveNext())
                    stack.Pop();
                else if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }

        private sealed class View : IDialogueView
        {
            public readonly List<string> Lines = new();

            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                Lines.Add(text);
                yield break;
            }

            public IEnumerator ShowChoice(
                IReadOnlyList<ChoiceOption> options,
                Action<string> selected
            )
            {
                selected("yes");
                yield break;
            }

            public IEnumerator ShowItemGet(string key, string message)
            {
                yield break;
            }

            public IEnumerator ShowWeaponGet(string key, string message)
            {
                yield break;
            }

            public IEnumerator RunCommand(string command, string argument)
            {
                yield break;
            }

            public IEnumerator Close()
            {
                yield break;
            }
        }

        private sealed class Items : IItemGiver
        {
            public bool Owned;

            public void Give(string key) { }

            public bool HasImportantItem(string key) => Owned;
        }

        private static readonly EventImporter.ImportCatalog Catalog = new(
            new[] { "card_key" },
            new[] { "card_key" }
        );

        private static EventDefinition Import(string steps)
        {
            var report = EventImporter.Parse(Document(steps), Catalog);
            Assert.False(report.HasErrors, string.Join(";", report.Diagnostics));
            return report.Events.Single();
        }

        [TestCase("yes", true)]
        [TestCase("no", false)]
        [TestCase("unknown", false)]
        public void FlagBranch_JoinsWithoutRunningOtherArms(string value, bool expected)
        {
            var ev = Import(
                @"{""kind"":""branch"",""flag"":""answer"",""cases"":{""yes"":[{""kind"":""line"",""text"":""yes""}],""no"":[{""kind"":""line"",""text"":""no""}]}},{""kind"":""line"",""text"":""joined""}"
            );
            var go = new GameObject("BranchPlayer");
            var player = go.AddComponent<EventPlayer>();
            var view = new View();
            try
            {
                var copy = ScriptableObject.CreateInstance<EventDefinition>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ev), copy);
                var progress = go.AddComponent<ProgressManager>();
                progress.SetFlag("answer", value);
                player.Inject(progress, view, new Items());
                Drive(player.PlayRoutine(copy));
                CollectionAssert.AreEqual(
                    value == "unknown" ? new[] { "joined" } : new[] { value, "joined" },
                    view.Lines
                );
                UnityEngine.Object.DestroyImmediate(copy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
                EventPlaybackService.SetPlaying(false);
            }
        }

        [TestCase(true, "owned")]
        [TestCase(false, "missing")]
        public void ItemBranch_UsesImportantItemLookup(bool owned, string expected)
        {
            var ev = Import(
                @"{""kind"":""branch"",""hasItem"":""card_key"",""then"":[{""kind"":""line"",""text"":""owned""}],""else"":[{""kind"":""line"",""text"":""missing""}]} ,{""kind"":""line"",""text"":""joined""}"
            );
            var go = new GameObject("BranchPlayer");
            var player = go.AddComponent<EventPlayer>();
            var view = new View();
            try
            {
                player.Inject(
                    go.AddComponent<ProgressManager>(),
                    view,
                    new Items { Owned = owned }
                );
                Drive(player.PlayRoutine(ev));
                CollectionAssert.AreEqual(new[] { expected, "joined" }, view.Lines);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
                EventPlaybackService.SetPlaying(false);
            }
        }

        [Test]
        public void Choice_SetsFlag_AndMissingArmsAreSkipped()
        {
            var ev = Import(
                @"{""kind"":""choice"",""flag"":""answer"",""options"":[{""text"":""Yes"",""value"":""yes""},{""text"":""No"",""value"":""no""}]},{""kind"":""branch"",""flag"":""answer"",""cases"":{""yes"":[{""kind"":""line"",""text"":""picked""}],""no"":[]}},{""kind"":""branch"",""hasItem"":""card_key""},{""kind"":""line"",""text"":""joined""}"
            );
            var go = new GameObject("BranchPlayer");
            var player = go.AddComponent<EventPlayer>();
            var view = new View();
            var progress = go.AddComponent<ProgressManager>();
            try
            {
                player.Inject(progress, view, new Items());
                Drive(player.PlayRoutine(ev));
                Assert.AreEqual("yes", progress.GetFlag("answer"));
                CollectionAssert.AreEqual(new[] { "picked", "joined" }, view.Lines);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
                EventPlaybackService.SetPlaying(false);
            }
        }

        [TestCase(@"{""kind"":""branch"",""flag"":""x"",""hasItem"":""card_key"",""cases"":{}}")]
        [TestCase(
            @"{""kind"":""branch"",""flag"":""x"",""cases"":{""a"":[{""kind"":""branch"",""hasItem"":""card_key""}]}}"
        )]
        [TestCase(
            @"{""kind"":""branch"",""flag"":""x"",""cases"":{""a"":[{""kind"":""choice""}]}}"
        )]
        [TestCase(@"{""kind"":""branch"",""hasItem"":""apple""}")]
        [TestCase(@"{""kind"":""branch"",""flag"":""x"",""cases"":{""a"":[],""a"":[]}}")]
        public void InvalidBranch_IsRejected(string step) =>
            Assert.True(EventImporter.Parse(Document(step), Catalog).HasErrors);

        [Test]
        public void CaseKeys_WarnForMissingAndExtra()
        {
            var report = EventImporter.Parse(
                Document(
                    @"{""kind"":""choice"",""flag"":""x"",""options"":[{""text"":""A"",""value"":""a""},{""text"":""B"",""value"":""b""}]},{""kind"":""branch"",""flag"":""x"",""cases"":{""a"":[],""c"":[]}}"
                ),
                Catalog
            );
            Assert.False(report.HasErrors);
            Assert.AreEqual(2, report.WarningCount);
            foreach (var ev in report.Events)
                UnityEngine.Object.DestroyImmediate(ev);
        }

        [TestCase(
            @"{""kind"":""branch"",""hasItem"":""card_key"",""then"":[{""kind"":""line"",""text"":""前""},{""kind"":""battle""}],""else"":[{""kind"":""line"",""text"":""別""}]}"
        )]
        [TestCase(
            @"{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""battle""}]}}"
        )]
        public void BattleInsideBranch_IsRejected(string branch)
        {
            var report = EventImporter.Parse(
                Document(@"{""kind"":""line"",""text"":""開始""}," + branch),
                Catalog
            );
            Assert.True(report.HasErrors);
            StringAssert.Contains("steps[1]", string.Join(";", report.Diagnostics));
            StringAssert.Contains("battle", string.Join(";", report.Diagnostics));
        }

        [Test]
        public void BattleFollowedByOptionalBranch_IsRejected()
        {
            var report = EventImporter.Parse(
                Document(
                    @"{""kind"":""line"",""text"":""開始""},{""kind"":""battle""},{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""line"",""text"":""終了""}]}}"
                ),
                Catalog
            );
            Assert.True(report.HasErrors);
        }

        [Test]
        public void ErrorAfterBranch_UsesOriginalJsonIndex()
        {
            var report = EventImporter.Parse(
                Document(
                    @"{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""line"",""text"":""正常""}],""no"":[]}},{""kind"":""line""}"
                ),
                Catalog
            );
            Assert.True(report.HasErrors);
            StringAssert.Contains("steps[1] line", string.Join(";", report.Diagnostics));
        }

        [Test]
        public void ErrorInsideBranch_UsesOriginalJsonPath()
        {
            var report = EventImporter.Parse(
                Document(
                    @"{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""line""}]}}"
                ),
                Catalog
            );
            Assert.True(report.HasErrors);
            StringAssert.Contains("steps[0].cases.yes[0]", string.Join(";", report.Diagnostics));
        }

        [Test]
        public void InvalidHasItemBranch_ReportsStepNotCondition()
        {
            var report = EventImporter.Parse(
                Document(
                    @"{""kind"":""line"",""text"":""開始""},{""kind"":""branch"",""hasItem"":""apple""}"
                ),
                Catalog
            );
            Assert.True(report.HasErrors);
            var diagnostics = string.Join(";", report.Diagnostics);
            StringAssert.Contains("steps[1] hasItem", diagnostics);
            StringAssert.DoesNotContain("conditions[", diagnostics);
        }

        [Test]
        public void PreviewDriver_IsolatesFlagsItemsAndProgress()
        {
            var ev = Import(
                @"{""kind"":""choice"",""flag"":""answer"",""options"":[{""text"":""はい"",""value"":""yes""},{""text"":""いいえ"",""value"":""no""}]},{""kind"":""giveItem"",""itemKey"":""card_key""},{""kind"":""branch"",""hasItem"":""card_key"",""then"":[{""kind"":""line"",""text"":""所持""}]},{""kind"":""branch"",""flag"":""answer"",""cases"":{""yes"":[{""kind"":""line"",""text"":""選択""}]}}"
            );
            var go = new GameObject("会話プレビューテスト");
            try
            {
                var progress = go.AddComponent<ProgressManager>();
                progress.SetFlag("answer", "本番の値");
                var driver = go.AddComponent<ConversationPreviewDriver>();
                var view = new View();
                Drive(driver.PlayEventPreview(ev, view));
                CollectionAssert.AreEqual(new[] { "所持", "選択" }, view.Lines);
                Assert.AreEqual("本番の値", progress.GetFlag("answer"));
                Assert.AreEqual(0, progress.Progress);
                Assert.False(EventPlaybackService.IsPlaying);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
                EventPlaybackService.SetPlaying(false);
            }
        }
    }
}
