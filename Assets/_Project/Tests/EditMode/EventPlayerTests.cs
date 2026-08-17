using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
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

            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                Lines.Add(text);
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

            public IEnumerator Run(BattleSetup setup)
            {
                Fought.Add(setup.EnemyPrefab);
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
            UnityEngine.Object.DestroyImmediate(_pmGo);
            UnityEngine.Object.DestroyImmediate(_epGo);
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
            Assert.AreEqual(6, _pm.Progress);
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

        [Test]
        public void PlayRoutine_NoNextProgress_IsInvalidData_DoesNotAdvance()
        {
            // 仕様(ScenarioReference.md フィールド表)では nextProgress は全イベント必須で、
            // Importer が省略を弾く(EventImporterTests.Parse_OmittedNextProgress_IsError)。
            // ここで固定するのは「万一 nextProgress 無しの定義を渡されても進行度を壊さない」
            // フォールバック挙動であって、「nextProgress を省略できる」という仕様ではない。
            var ev = EventDefinition.Create(
                "invalid_no_next_progress",
                new[] { EventCondition.Progress(0) },
                new[]
                {
                    EventStep.Line("はかなげ少女", "girl_smile", "ここまで一緒に来られたね。"),
                },
                nextProgress: null
            );

            Drive(_player.PlayRoutine(ev));

            CollectionAssert.AreEqual(new[] { "ここまで一緒に来られたね。" }, _view.Lines);
            Assert.AreEqual(0, _pm.Progress); // 進めない
        }
    }
}
