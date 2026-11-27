using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static CreativeAI.Tests.TestCoroutine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベント再生(会話・アイテム/武器付与・戦闘・進行度更新)の検証。
    /// </summary>
    public class EventPlayerTests
    {
        private sealed class FakeDialogueView : IDialogueView
        {
            public readonly List<string> Lines = new();
            public string ChoiceToReturn;

            // 台詞・ウィンドウを閉じる・戦闘の順序を見るための記録(戦闘は FakeEventBattle が書き込む)。
            public readonly List<string> Timeline = new();

            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                Lines.Add(text);
                Timeline.Add(text);
                yield break;
            }

            public IEnumerator Close()
            {
                Timeline.Add("close");
                yield break;
            }

            public bool HiddenImmediately;

            public void HideImmediate() => HiddenImmediately = true;

            public IEnumerator ShowChoice(
                IReadOnlyList<ChoiceOption> options,
                Action<string> onSelected
            )
            {
                onSelected?.Invoke(ChoiceToReturn);
                yield break;
            }

            public readonly List<string> ItemGets = new();
            public readonly List<string> WeaponGets = new();
            public readonly List<string> Commands = new();

            public IEnumerator ShowItemPicker(string prompt, Action<string> onPicked)
            {
                onPicked?.Invoke(null);
                yield break;
            }

            public IEnumerator ShowItemGet(string itemKey, string message)
            {
                ItemGets.Add(itemKey);
                yield break;
            }

            public IEnumerator ShowWeaponGet(string weaponKey, string message)
            {
                WeaponGets.Add(weaponKey);
                yield break;
            }

            public IEnumerator RunCommand(string command, string argument)
            {
                Commands.Add(argument == null ? command : $"{command}:{argument}");
                yield break;
            }
        }

        private sealed class FakeItemGiver : IItemGiver
        {
            public readonly List<string> Given = new();
            public readonly HashSet<string> Owned = new();

            public void Give(string itemKey) => Given.Add(itemKey);

            public bool HasImportantItem(string itemKey) => Owned.Contains(itemKey);

            public int CountItem(string itemKey) => 0;

            public bool Consume(string itemKey, int count) => false;
        }

        private sealed class FakeWeaponGiver : IWeaponGiver
        {
            public readonly List<string> Given = new();

            public void GiveWeapon(string weaponKey) => Given.Add(weaponKey);
        }

        private sealed class FakeEventBattle : IEventBattle
        {
            public readonly List<string> Fought = new();
            public Action OnRun; // 戦闘中に起きることを差し込む
            public bool Hold; // true の間は決着しない(戦闘の最中で止めておく)

            public IEnumerator Fight(string encounterId)
            {
                Fought.Add(encounterId);
                OnRun?.Invoke();
                while (Hold)
                    yield return null;
            }
        }

        private GameObject _pmGo;
        private ProgressManager _pm;
        private EventPlayer _player;
        private FakeDialogueView _view;
        private FakeItemGiver _items;

        [SetUp]
        public void SetUp()
        {
            _pmGo = new GameObject("PM");
            _pm = _pmGo.AddComponent<ProgressManager>();
            _view = new FakeDialogueView();
            _items = new FakeItemGiver();
            _player = new EventPlayer(_pm, view: _view, items: _items);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pmGo);
        }

        private static EventDefinition OneLineEvent(
            string id,
            int progress,
            string text,
            int next
        ) =>
            EventDefinition.Create(
                id,
                new[] { EventCondition.Progress(progress) },
                new[] { EventStep.Line(null, null, text) },
                next
            );

        private static EventDefinition BattleEvent() =>
            EventDefinition.Create(
                "boss",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line(null, null, "来るわ!"),
                    EventStep.Battle("boss_arena"),
                    EventStep.Line(null, null, "倒した!"),
                },
                6
            );

        [Test]
        public void Play_GiveItemWithoutInventory_WarnsButStillShowsPresentation()
        {
            // プレビューシーンのように所持品が居ない場面。演出だけ出て所持品に入らないことを警告で知らせる。
            _player = new EventPlayer(_pm, view: _view);
            var ev = EventDefinition.Create(
                "gift",
                new[] { EventCondition.Progress(0) },
                new[] { EventStep.GiveItem("apple") },
                1
            );
            LogAssert.Expect(LogType.Warning, new Regex("所持品が無い.*apple"));

            Drive(_player.Play(ev));

            CollectionAssert.AreEqual(new[] { "apple" }, _view.ItemGets, "入手演出は出す");
        }

        [Test]
        public void Play_SetFlagStep_WritesTheFlag()
        {
            _pm.SetFlag("crafted", "1");
            var ev = EventDefinition.Create(
                "reset",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.SetFlag("crafted", string.Empty),
                    EventStep.Line(null, null, "a"),
                },
                1
            );

            Drive(_player.Play(ev));

            Assert.AreEqual(string.Empty, _pm.GetFlag("crafted"));
        }

        private static EventDefinition RepeatEvent(string id, EventRepeat repeat) =>
            EventDefinition.Create(
                id,
                new[] { EventCondition.Progress(3) },
                new[] { EventStep.Line(null, null, "調べた") },
                0,
                repeat,
                advancesProgress: false
            );

        private static GameObject CreateSpawnPoint(string id, Vector3 position)
        {
            var go = new GameObject("TestSpawnPoint");
            go.transform.position = position;
            var so = new SerializedObject(go.AddComponent<SpawnPoint>());
            so.FindProperty("_id").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static EventDefinition MoveEvent(string spawnPointId) =>
            EventDefinition.Create(
                "pull_back",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line(null, null, "戻ろう"),
                    EventStep.Command(EventPlayerMovement.MoveCommand, spawnPointId),
                },
                1
            );

        [Test]
        public void Play_PlayerMoveTo_PlacesPlayerAtSpawnPoint()
        {
            var player = TestPlayer.Create();
            var point = CreateSpawnPoint("test_pull_back", new Vector3(3f, 0f, 4f));
            try
            {
                Drive(_player.Play(MoveEvent("test_pull_back")));

                Assert.AreEqual(point.transform.position, player.transform.position);
            }
            finally
            {
                TestPlayer.Destroy(player);
                UnityEngine.Object.DestroyImmediate(point);
            }
        }

        [Test]
        public void Play_PlayerMoveTo_MissingSpawnPoint_WarnsAndDoesNotMove()
        {
            var player = TestPlayer.Create();
            player.transform.position = new Vector3(1f, 2f, 3f);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("player.moveTo"));

                Drive(_player.Play(MoveEvent("no_such_point")));

                Assert.AreEqual(
                    new Vector3(1f, 2f, 3f),
                    player.transform.position,
                    "原点へ飛ばさない"
                );
                Assert.AreEqual(1, _pm.Progress, "イベント自体は最後まで進む");
            }
            finally
            {
                TestPlayer.Destroy(player);
            }
        }

        [Test]
        public void Play_Once_LeavesSeenFlagAndKeepsProgress()
        {
            _pm.AdvanceTo(3);
            var ev = RepeatEvent("drawer_once", EventRepeat.Once);

            Drive(_player.Play(ev));

            Assert.AreEqual(
                ProgressFlags.SeenValue,
                _pm.GetFlag(ProgressFlags.Seen("drawer_once"))
            );
            Assert.AreEqual(3, _pm.Progress, "進行度は進めない");
            Assert.IsFalse(
                ev.ConditionsMet(_pm.Progress, _pm.GetFlag),
                "見終えたら二度と発火しない"
            );
        }

        [Test]
        public void Play_Always_LeavesNoFlagAndStaysAvailable()
        {
            _pm.AdvanceTo(3);
            var ev = RepeatEvent("shelf_check", EventRepeat.Always);

            Drive(_player.Play(ev));

            Assert.AreEqual(string.Empty, _pm.GetFlag(ProgressFlags.Seen("shelf_check")));
            Assert.AreEqual(3, _pm.Progress);
            Assert.IsTrue(ev.ConditionsMet(_pm.Progress, _pm.GetFlag), "何度でも発火できる");
        }

        [Test]
        public void SeenFlag_SurvivesSaveAndLoad()
        {
            _pm.AdvanceTo(3);
            Drive(_player.Play(RepeatEvent("drawer_once", EventRepeat.Once)));

            // セーブされるのは Flags 全体。ロードで復元しても見終えた印が残る。
            var saved = new System.Collections.Generic.Dictionary<string, string>(_pm.Flags);
            _pm.LoadState(0, null);
            Assert.AreEqual(string.Empty, _pm.GetFlag(ProgressFlags.Seen("drawer_once")));
            _pm.LoadState(3, saved);

            Assert.AreEqual(
                ProgressFlags.SeenValue,
                _pm.GetFlag(ProgressFlags.Seen("drawer_once"))
            );
        }

        [Test]
        public void Play_ClosesWindowAtEnd()
        {
            Drive(_player.Play(OneLineEvent("a", 0, "ありがとう", 1)));

            CollectionAssert.AreEqual(new[] { "ありがとう", "close" }, _view.Timeline);
        }

        [Test]
        public void Play_ClosesWindowBeforeBattle()
        {
            var runner = new FakeEventBattle();
            runner.OnRun = () => _view.Timeline.Add("battle");
            _player = new EventPlayer(_pm, view: _view, items: _items, battle: runner);

            Drive(_player.Play(BattleEvent()));

            CollectionAssert.AreEqual(
                new[] { "来るわ!", "close", "battle", "倒した!", "close" },
                _view.Timeline
            );
        }

        [Test]
        public void Abort_DuringBattle_StopsWithoutAdvancing()
        {
            // 戦闘に負けてセーブ再開(シーン再読込)すると、再生を回していたトリガーごと消えて打ち切られる。
            var gmmGo = new GameObject("GMM");
            var gmm = gmmGo.AddComponent<GameModeManager>();
            var runner = new FakeEventBattle { Hold = true };
            _player = new EventPlayer(_pm, gmm, _view, _items, battle: runner);
            try
            {
                var routine = _player.Play(BattleEvent());
                DriveUntil(routine, () => runner.Fought.Count > 0);

                LogAssert.Expect(LogType.Warning, new Regex("打ち切りました"));
                _player.Abort(); // コルーチンは発火側ごと止まるので、この先は回さない

                CollectionAssert.DoesNotContain(_view.Lines, "倒した!", "戦闘後の台詞は流さない");
                Assert.AreEqual(0, _pm.Progress, "進行度は進めない(再開後にもう一度発火できる)");
                Assert.IsTrue(_view.HiddenImmediately, "会話ウィンドウはその場で消す");
                Assert.AreEqual(GameMode.Field, gmm.Mode, "フィールドへ戻す");
                Assert.IsFalse(_player.IsPlaying, "再生中フラグは戻す");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmmGo);
            }
        }

        [Test]
        public void Abort_ThenPlay_RunsTheNextEvent()
        {
            var runner = new FakeEventBattle { Hold = true };
            _player = new EventPlayer(_pm, view: _view, items: _items, battle: runner);
            DriveUntil(_player.Play(BattleEvent()), () => runner.Fought.Count > 0);
            LogAssert.Expect(LogType.Warning, new Regex("打ち切りました"));
            _player.Abort();

            Drive(_player.Play(OneLineEvent("a", 0, "A", 1)));

            Assert.AreEqual(1, _pm.Progress, "打ち切ったあとも次のイベントは再生できる");
        }

        [Test]
        public void Abort_WhenIdle_DoesNothing()
        {
            _player.Abort();

            Assert.IsFalse(_view.HiddenImmediately, "再生していなければ会話ウィンドウに触れない");
        }

        [Test]
        public void Play_OutsideFieldMode_IgnoresEvent()
        {
            // 別の EventPlayer が再生中(会話モード)なら、この EventPlayer も始めない。
            var gmmGo = new GameObject("GMM");
            var gmm = gmmGo.AddComponent<GameModeManager>();
            gmm.TryStartDialogue();
            _player = new EventPlayer(_pm, gmm, _view, _items);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("無視しました"));

                Drive(_player.Play(OneLineEvent("a", 0, "A", 1)));

                CollectionAssert.IsEmpty(_view.Lines);
                Assert.AreEqual(0, _pm.Progress);
                Assert.AreEqual(GameMode.Dialogue, gmm.Mode, "再生中のモードを戻さない");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmmGo);
            }
        }

        [Test]
        public void Play_IsPlayingDuringPlayback_ClearsAfter()
        {
            var routine = _player.Play(OneLineEvent("a", 0, "A", 1));

            routine.MoveNext(); // 最初の台詞まで進める
            Assert.IsTrue(_player.IsPlaying, "再生中");

            Drive(routine);
            Assert.IsFalse(_player.IsPlaying, "終了後は戻る");
        }

        [Test]
        public void Play_WhileAnotherIsPlaying_IgnoresSecond()
        {
            var first = _player.Play(OneLineEvent("first", 0, "1本目", 1));
            first.MoveNext(); // 1本目を再生中にする

            Drive(_player.Play(OneLineEvent("second", 0, "2本目", 5)));

            CollectionAssert.DoesNotContain(_view.Lines, "2本目", "2本目は再生しない");
            Assert.IsTrue(_player.IsPlaying, "2本目が1本目の再生中フラグを戻さない");

            Drive(first);
            Assert.AreEqual(1, _pm.Progress, "進行度は1本目の nextProgress だけ反映");
            Assert.IsFalse(_player.IsPlaying);
        }

        [Test]
        public void Play_AnotherPlayerIsPlaying_IgnoresEvenInFieldMode()
        {
            // 別の発火元の EventPlayer が再生中なら、モードが Field に戻っていても2本目は始めない。
            var gmmGo = new GameObject("GMM");
            try
            {
                var gmm = gmmGo.AddComponent<GameModeManager>();
                _player = new EventPlayer(_pm, gmm, _view, _items);
                var other = new EventPlayer(_pm, gmm, _view, _items);
                var first = _player.Play(OneLineEvent("first", 0, "1本目", 1));
                first.MoveNext();
                gmm.ResetToField(); // 途中で誰かがモードを戻した

                Drive(other.Play(OneLineEvent("second", 0, "2本目", 5)));

                CollectionAssert.DoesNotContain(_view.Lines, "2本目");
                Assert.AreSame(_player, EventPlayer.Playing);
                Assert.IsFalse(other.IsPlaying);

                Drive(first);
                Assert.IsNull(EventPlayer.Playing, "終わったら再生中を空ける");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmmGo);
            }
        }

        [Test]
        public void Play_RunsLinesInOrder_GivesItem_SetsFlag_Advances()
        {
            _view.ChoiceToReturn = "together";
            var ev = EventDefinition.Create(
                "cave_encounter",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("主人公", "hero_surprised", "…誰だ?"),
                    EventStep.GiveItem("old_key"),
                    EventStep.Choice(
                        "girl_choice",
                        new ChoiceOption("一緒に行く", "together"),
                        new ChoiceOption("ひとりで行く", "alone")
                    ),
                    EventStep.Line("主人公", "hero_normal", "…そうか。"),
                },
                nextProgress: 6
            );

            Drive(_player.Play(ev));

            CollectionAssert.AreEqual(new[] { "…誰だ?", "…そうか。" }, _view.Lines);
            CollectionAssert.AreEqual(new[] { "old_key" }, _items.Given);
            // 在庫に入れるだけでなく、会話UIの入手演出も回す。
            CollectionAssert.AreEqual(new[] { "old_key" }, _view.ItemGets);
            Assert.AreEqual("together", _pm.GetFlag("girl_choice"));
            Assert.AreEqual(6, _pm.Progress);
        }

        [Test]
        public void Play_GiveWeaponStep_RoutesToWeaponGiver()
        {
            var weapons = new FakeWeaponGiver();
            _player = new EventPlayer(_pm, view: _view, items: _items, weapons: weapons);

            var ev = EventDefinition.Create(
                "girl_gift",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("はかなげ少女", "girl_resolve", "これで、身を守って。"),
                    EventStep.GiveWeapon("scythe"),
                    EventStep.Line("主人公", "hero_normal", "…ありがとう。"),
                },
                nextProgress: 6
            );

            Drive(_player.Play(ev));

            CollectionAssert.AreEqual(new[] { "scythe" }, weapons.Given);
            CollectionAssert.AreEqual(new[] { "scythe" }, _view.WeaponGets);
            Assert.AreEqual(6, _pm.Progress);
        }

        [Test]
        public void Play_CommandStep_RoutesToDialogueView()
        {
            var ev = EventDefinition.Create(
                "shaken",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("主人公", "hero_surprised", "地面が揺れた。"),
                    EventStep.Command("portrait.left.shake"),
                    EventStep.Command("wait", "0.5"),
                    EventStep.Line("主人公", "hero_normal", "…収まったか。"),
                },
                nextProgress: 6
            );

            Drive(_player.Play(ev));

            CollectionAssert.AreEqual(new[] { "portrait.left.shake", "wait:0.5" }, _view.Commands);
        }

        [Test]
        public void Play_BattleStep_RunsRunnerWithArena_ThenContinues()
        {
            var battle = new FakeEventBattle();
            _player = new EventPlayer(_pm, view: _view, items: _items, battle: battle);

            var ev = EventDefinition.Create(
                "cave_encounter",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("主人公", "hero_surprised", "…誰だ?"),
                    EventStep.Battle("wolf_boss_arena"),
                    EventStep.Line("はかなげ少女", "girl_resolve", "……ありがとう。"),
                },
                nextProgress: 6
            );

            Drive(_player.Play(ev));

            // events.json の arena で指した名前がそのまま戦闘役に渡る
            CollectionAssert.AreEqual(new[] { "wolf_boss_arena" }, battle.Fought);
            // 戦闘を挟んで会話が続き、最後まで再生される
            CollectionAssert.AreEqual(new[] { "…誰だ?", "……ありがとう。" }, _view.Lines);
            Assert.AreEqual(6, _pm.Progress);
        }
    }
}
