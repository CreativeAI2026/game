using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.DevTools;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static CreativeAI.Tests.TestCoroutine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// events.json の分岐ステップ(フラグ・所持品・選択肢)の取り込みと再生の検証。
    /// 不正な分岐の拒否、エラー位置の報告、プレビュー実行が本番の進行を汚さないことまで見る。
    /// </summary>
    public class EventBranchTests
    {
        private static string Document(string steps) =>
            "{\"events\":[{\"id\":\"branch_test\",\"conditions\":[{\"type\":\"progress\",\"value\":0}],\"nextProgress\":1,\"steps\":["
            + steps
            + "]}]}";

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

            public IEnumerator ShowItemPicker(string prompt, Action<string> onPicked)
            {
                onPicked?.Invoke(null);
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

            public void HideImmediate() { }
        }

        private sealed class Items : IItemGiver
        {
            public bool Owned;

            public void Give(string key) { }

            public bool HasImportantItem(string key) => Owned;

            public int CountItem(string key) => 0;

            public bool Consume(string key, int count) => false;
        }

        private sealed class MemoryProgress : IEventProgress
        {
            public readonly Dictionary<string, string> Flags = new();
            public int Progress;

            public string GetFlag(string key) =>
                Flags.TryGetValue(key, out var value) ? value : string.Empty;

            public void SetFlag(string key, string value) => Flags[key] = value;

            public void AdvanceTo(int progress) => Progress = progress;
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
            var view = new View();
            try
            {
                var copy = ScriptableObject.CreateInstance<EventDefinition>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ev), copy);
                var progress = go.AddComponent<ProgressManager>();
                progress.SetFlag("answer", value);
                var player = new EventPlayer(progress, view: view, items: new Items());
                Drive(player.Play(copy));
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
            var view = new View();
            try
            {
                var player = new EventPlayer(
                    go.AddComponent<ProgressManager>(),
                    view: view,
                    items: new Items { Owned = owned }
                );
                Drive(player.Play(ev));
                CollectionAssert.AreEqual(new[] { expected, "joined" }, view.Lines);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Choice_SetsFlag_AndMissingArmsAreSkipped()
        {
            var ev = Import(
                @"{""kind"":""choice"",""flag"":""answer"",""options"":[{""text"":""Yes"",""value"":""yes""},{""text"":""No"",""value"":""no""}]},{""kind"":""branch"",""flag"":""answer"",""cases"":{""yes"":[{""kind"":""line"",""text"":""picked""}],""no"":[]}},{""kind"":""branch"",""hasItem"":""card_key""},{""kind"":""line"",""text"":""joined""}"
            );
            var go = new GameObject("BranchPlayer");
            var view = new View();
            var progress = go.AddComponent<ProgressManager>();
            try
            {
                var player = new EventPlayer(progress, view: view, items: new Items());
                Drive(player.Play(ev));
                Assert.AreEqual("yes", progress.GetFlag("answer"));
                CollectionAssert.AreEqual(new[] { "picked", "joined" }, view.Lines);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
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
            @"{""kind"":""branch"",""hasItem"":""card_key"",""then"":[{""kind"":""line"",""text"":""前""},{""kind"":""battle"",""arena"":""preview""}],""else"":[{""kind"":""line"",""text"":""別""}]}"
        )]
        [TestCase(
            @"{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""battle"",""arena"":""preview""}]}}"
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
                    @"{""kind"":""line"",""text"":""開始""},{""kind"":""battle"",""arena"":""preview""},{""kind"":""branch"",""flag"":""x"",""cases"":{""yes"":[{""kind"":""line"",""text"":""終了""}]}}"
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
                var originalView = new View();
                var player = new EventPlayer(progress, view: originalView, items: new Items());
                var driver = go.AddComponent<ConversationPreviewDriver>();
                var view = new View();
                Drive(driver.PlayEventPreview(ev, view));
                CollectionAssert.AreEqual(new[] { "所持", "選択" }, view.Lines);
                Assert.AreEqual("本番の値", progress.GetFlag("answer"));
                Assert.AreEqual(0, progress.Progress);
                Drive(player.Play(ev));
                CollectionAssert.AreEqual(new[] { "選択" }, originalView.Lines);
                Assert.AreEqual("yes", progress.GetFlag("answer"));
                Assert.AreEqual(1, progress.Progress);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EventPlayer_UsesGivenProgressForFlagsAndCompletion()
        {
            var ev = Import(
                @"{""kind"":""choice"",""flag"":""answer"",""options"":[{""text"":""はい"",""value"":""yes""},{""text"":""いいえ"",""value"":""no""}]},{""kind"":""branch"",""flag"":""answer"",""cases"":{""yes"":[{""kind"":""line"",""text"":""選択""}]}}"
            );
            var go = new GameObject("注入状態のテスト");
            try
            {
                var production = go.AddComponent<ProgressManager>();
                production.SetFlag("answer", "本番の値");
                var state = new MemoryProgress();
                var view = new View();
                var player = new EventPlayer(state, view: view, items: new Items());
                Drive(player.Play(ev));
                Assert.AreEqual("yes", state.GetFlag("answer"));
                Assert.AreEqual(1, state.Progress);
                CollectionAssert.AreEqual(new[] { "選択" }, view.Lines);
                Assert.AreEqual("本番の値", production.GetFlag("answer"));
                Assert.AreEqual(0, production.Progress);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreviewDriver_BattleDoesNotChangeProductionMode()
        {
            var ev = Import(
                @"{""kind"":""line"",""text"":""戦闘前""},{""kind"":""battle"",""arena"":""preview""},{""kind"":""line"",""text"":""戦闘後""}"
            );
            var go = new GameObject("戦闘プレビューのテスト");
            try
            {
                var progress = go.AddComponent<ProgressManager>();
                var mode = go.AddComponent<GameModeManager>();
                mode.TryStartBattle();
                int modeChanges = 0;
                mode.Changed += () => modeChanges++;
                var driver = go.AddComponent<ConversationPreviewDriver>();
                var view = new View();
                Drive(driver.PlayEventPreview(ev, view));
                CollectionAssert.AreEqual(new[] { "戦闘前", "戦闘後" }, view.Lines);
                Assert.AreEqual(GameMode.Battle, mode.Mode);
                Assert.AreEqual(0, modeChanges);
                Assert.AreEqual(0, progress.Progress);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ev);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
