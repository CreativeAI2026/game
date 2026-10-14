using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベント再生(会話・アイテム/武器付与・戦闘・進行度更新)の検証。
    /// </summary>
    public class EventPlayerTests
    {
        // yield return <IEnumerator> のネストを Unity 同様に展開しながら同期駆動する。
        // fake は即座に yield break するので待ち時間は発生しない。
        private static void Drive(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator nested)
                        stack.Push(nested);
                }
                else
                {
                    stack.Pop();
                }
            }
        }

        private sealed class FakeDialogueView : IDialogueView
        {
            public readonly List<string> Lines = new();
            public string ChoiceToReturn;

            // 台詞・ウィンドウを閉じる・戦闘の順序を見るための記録(戦闘は FakeBattleRunner が書き込む)。
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
        }

        private sealed class FakeWeaponGiver : IWeaponGiver
        {
            public readonly List<string> Given = new();

            public void GiveWeapon(string weaponKey) => Given.Add(weaponKey);
        }

        private sealed class FakeBattleRunner : IBattleRunner
        {
            public readonly List<GameObject> Fought = new();
            public Action OnRun; // 戦闘中に起きること(シーン切替など)を差し込む

            public IEnumerator Run(BattleSetup setup)
            {
                Fought.Add(setup.EnemyPrefab);
                OnRun?.Invoke();
                yield break;
            }
        }

        private GameObject _pmGo;
        private GameObject _epGo;
        private ProgressManager _pm;
        private EventPlayer _player;
        private FakeDialogueView _view;
        private FakeItemGiver _items;

        [SetUp]
        public void SetUp()
        {
            _pmGo = new GameObject("PM");
            _pm = _pmGo.AddComponent<ProgressManager>();
            _epGo = new GameObject("EP");
            _player = _epGo.AddComponent<EventPlayer>();
            _view = new FakeDialogueView();
            _items = new FakeItemGiver();
            _player.Inject(_pm, _view, _items);
        }

        [TearDown]
        public void TearDown()
        {
            EventPlaybackService.SetPlaying(false);
            UnityEngine.Object.DestroyImmediate(_pmGo);
            UnityEngine.Object.DestroyImmediate(_epGo);
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
                    EventStep.Battle(),
                    EventStep.Line(null, null, "倒した!"),
                },
                6
            );

        [Test]
        public void PlayRoutine_GiveItemWithoutInventory_WarnsButStillShowsPresentation()
        {
            // プレビューシーンのように所持品が居ない場面。演出だけ出て所持品に入らないことを警告で知らせる。
            ItemGiverService.Current = null;
            _player.Inject(_pm, _view, null);
            var ev = EventDefinition.Create(
                "gift",
                new[] { EventCondition.Progress(0) },
                new[] { EventStep.GiveItem("apple") },
                1
            );
            LogAssert.Expect(LogType.Warning, new Regex("IItemGiver 未登録.*apple"));

            Drive(_player.PlayRoutine(ev));

            CollectionAssert.AreEqual(new[] { "apple" }, _view.ItemGets, "入手演出は出す");
        }

        [Test]
        public void PlayRoutine_ClosesWindowAtEnd()
        {
            Drive(_player.PlayRoutine(OneLineEvent("a", 0, "ありがとう", 1)));

            CollectionAssert.AreEqual(new[] { "ありがとう", "close" }, _view.Timeline);
        }

        [Test]
        public void PlayRoutine_ClosesWindowBeforeBattle()
        {
            var runner = new FakeBattleRunner();
            runner.OnRun = () => _view.Timeline.Add("battle");
            _player.Inject(_pm, _view, _items, runner);
            var enemy = new GameObject("enemy");

            Drive(
                _player.PlayRoutine(
                    BattleEvent(),
                    new BattleSetup(enemy, Vector3.zero, Quaternion.identity)
                )
            );

            CollectionAssert.AreEqual(
                new[] { "来るわ!", "close", "battle", "倒した!", "close" },
                _view.Timeline
            );
            UnityEngine.Object.DestroyImmediate(enemy);
        }

        [Test]
        public void PlayRoutine_SceneChangedDuringBattle_AbortsWithoutAdvancing()
        {
            // 戦闘に負けてセーブ再開(シーン再読込)した状況。敵が消えて battle が抜けても続きを流さない。
            var runner = new FakeBattleRunner();
            runner.OnRun = () =>
                TestReflection.Invoke(
                    _player,
                    "OnActiveSceneChanged",
                    default(Scene),
                    default(Scene)
                );
            _player.Inject(_pm, _view, _items, runner);
            var enemy = new GameObject("enemy");

            Drive(
                _player.PlayRoutine(
                    BattleEvent(),
                    new BattleSetup(enemy, Vector3.zero, Quaternion.identity)
                )
            );

            CollectionAssert.DoesNotContain(_view.Lines, "倒した!", "戦闘後の台詞は流さない");
            Assert.AreEqual(0, _pm.Progress, "進行度は進めない(再開後にもう一度発火できる)");
            Assert.AreEqual("close", _view.Timeline[^1], "会話ウィンドウは閉じる");
            Assert.IsFalse(EventPlaybackService.IsPlaying, "再生中フラグは戻す");
            UnityEngine.Object.DestroyImmediate(enemy);
        }

        [Test]
        public void PlayRoutine_SceneChangedBeforeStart_DoesNotAffectNextEvent()
        {
            // 打ち切りの判定は「再生開始後に」切り替わったかどうか。過去の切替は関係ない。
            TestReflection.Invoke(_player, "OnActiveSceneChanged", default(Scene), default(Scene));

            Drive(_player.PlayRoutine(OneLineEvent("a", 0, "A", 1)));

            Assert.AreEqual(1, _pm.Progress);
        }

        [Test]
        public void PlayRoutine_SetsPlayingFlagDuringPlayback_ClearsAfter()
        {
            var routine = _player.PlayRoutine(OneLineEvent("a", 0, "A", 1));

            routine.MoveNext(); // 最初の台詞まで進める
            Assert.IsTrue(EventPlaybackService.IsPlaying, "再生中");

            Drive(routine);
            Assert.IsFalse(EventPlaybackService.IsPlaying, "終了後は戻る");
        }

        [Test]
        public void PlayRoutine_WhileAnotherIsPlaying_IgnoresSecond()
        {
            var first = _player.PlayRoutine(OneLineEvent("first", 0, "1本目", 1));
            first.MoveNext(); // 1本目を再生中にする

            Drive(_player.PlayRoutine(OneLineEvent("second", 0, "2本目", 5)));

            CollectionAssert.DoesNotContain(_view.Lines, "2本目", "2本目は再生しない");
            Assert.IsTrue(EventPlaybackService.IsPlaying, "2本目が1本目の再生中フラグを戻さない");

            Drive(first);
            Assert.AreEqual(1, _pm.Progress, "進行度は1本目の nextProgress だけ反映");
            Assert.IsFalse(EventPlaybackService.IsPlaying);
        }

        [Test]
        public void PlayRoutine_RunsLinesInOrder_GivesItem_SetsFlag_Advances()
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

            Drive(_player.PlayRoutine(ev));

            CollectionAssert.AreEqual(new[] { "…誰だ?", "…そうか。" }, _view.Lines);
            CollectionAssert.AreEqual(new[] { "old_key" }, _items.Given);
            // 在庫に入れるだけでなく、会話UIの入手演出も回す。
            CollectionAssert.AreEqual(new[] { "old_key" }, _view.ItemGets);
            Assert.AreEqual("together", _pm.GetFlag("girl_choice"));
            Assert.AreEqual(6, _pm.Progress);
        }

        [Test]
        public void PlayRoutine_GiveWeaponStep_RoutesToWeaponGiver()
        {
            var weapons = new FakeWeaponGiver();
            _player.Inject(_pm, _view, _items, weapons: weapons);

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

            Drive(_player.PlayRoutine(ev));

            CollectionAssert.AreEqual(new[] { "scythe" }, weapons.Given);
            CollectionAssert.AreEqual(new[] { "scythe" }, _view.WeaponGets);
            Assert.AreEqual(6, _pm.Progress);
        }

        [Test]
        public void PlayRoutine_CommandStep_RoutesToDialogueView()
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

            Drive(_player.PlayRoutine(ev));

            CollectionAssert.AreEqual(new[] { "portrait.left.shake", "wait:0.5" }, _view.Commands);
        }

        [Test]
        public void PlayRoutine_BattleStep_EntersAndExitsBattle_RunsRunner_ThenContinues()
        {
            var gmmGo = new GameObject("GMM");
            var gmm = gmmGo.AddComponent<GameModeManager>();
            var modeChanges = new List<GameMode>();
            gmm.OnModeChanged += m => modeChanges.Add(m);
            var battle = new FakeBattleRunner();
            _player.Inject(_pm, _view, _items, battle, gmm);

            // 敵はトリガーが配線して BattleSetup で渡す(JSON には書かない)。
            var enemyPrefab = new GameObject("wolf_boss");
            var setup = new BattleSetup(enemyPrefab, Vector3.zero, Quaternion.identity);

            var ev = EventDefinition.Create(
                "cave_encounter",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("主人公", "hero_surprised", "…誰だ?"),
                    EventStep.Battle(),
                    EventStep.Line("はかなげ少女", "girl_resolve", "……ありがとう。"),
                },
                nextProgress: 6
            );

            Drive(_player.PlayRoutine(ev, setup));

            CollectionAssert.AreEqual(new[] { enemyPrefab }, battle.Fought);
            // battle ステップ前後で Battle → Field に遷移
            CollectionAssert.AreEqual(new[] { GameMode.Battle, GameMode.Field }, modeChanges);
            // 戦闘を挟んで会話が続き、最後まで再生される
            CollectionAssert.AreEqual(new[] { "…誰だ?", "……ありがとう。" }, _view.Lines);
            Assert.AreEqual(6, _pm.Progress);

            UnityEngine.Object.DestroyImmediate(enemyPrefab);
            UnityEngine.Object.DestroyImmediate(gmmGo);
        }
    }
}
