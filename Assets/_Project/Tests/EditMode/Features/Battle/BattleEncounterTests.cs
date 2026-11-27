using System.Text.RegularExpressions;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 戦闘(BattleEncounter)を名前で探して会話イベントから戦う入口と、決着したときのモードの戻し方の検証。
    /// </summary>
    public sealed class BattleEncounterTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _enemy;
        private GameObject _encounterGo;
        private BattleEncounter _encounter;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            // EditMode では Awake が自動で走らず、DontDestroyOnLoad も使えないので、Instance だけ直接入れる。
            TestReflection.SetStaticProperty<GameModeManager>("Instance", _gmm);

            _enemy = new GameObject("boss");
            _enemy.AddComponent<EnemyStatus>();

            _encounterGo = new GameObject("encounter");
            _encounterGo.AddComponent<BoxCollider>().isTrigger = true;
            _encounter = _encounterGo.AddComponent<BattleEncounter>();
            TestReflection.SetField(_encounter, "_enemies", new[] { _enemy });
            TestReflection.SetField(_encounter, "_encounterId", "test_encounter");
            // Awake で名前を登録して敵を隠すので、設定を入れてから呼ぶ。
            TestReflection.Invoke(_encounter, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode では OnDestroy も自動で走らない。名前の登録を残さないよう自分で呼ぶ。
            TestReflection.Invoke(_encounter, "OnDestroy");
            Object.DestroyImmediate(_encounterGo);
            Object.DestroyImmediate(_enemy);
            Object.DestroyImmediate(_gmmGo);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
        }

        [Test]
        public void Find_ReturnsTheEncounterWithThatName()
        {
            Assert.AreSame(_encounter, BattleEncounter.Find("test_encounter"));
        }

        [Test]
        public void Find_UnknownName_ReturnsNull()
        {
            Assert.IsNull(BattleEncounter.Find("no_such_encounter"));
        }

        [Test]
        public void Find_AfterDestroy_ReturnsNull()
        {
            TestReflection.Invoke(_encounter, "OnDestroy");

            Assert.IsNull(BattleEncounter.Find("test_encounter"));
        }

        [Test]
        public void Awake_HidesTheEnemyUntilTheBattleStarts()
        {
            Assert.IsFalse(_enemy.activeSelf);
            Assert.IsFalse(_encounter.HasStarted);
        }

        [Test]
        public void StartFromEvent_ShowsEnemy_EntersBattle_AndNotifies()
        {
            int started = 0;
            _encounter.BattleStarted += () => started++;

            bool result = _encounter.StartFromEvent();

            Assert.IsTrue(result);
            Assert.IsTrue(_enemy.activeSelf);
            Assert.IsTrue(_encounter.HasStarted);
            Assert.AreEqual(GameMode.Battle, _gmm.Mode);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void StartFromEvent_WhenAlreadyStarted_DoesNothing()
        {
            int started = 0;
            _encounter.StartFromEvent();
            _encounter.BattleStarted += () => started++;

            bool result = _encounter.StartFromEvent();

            Assert.IsFalse(result);
            Assert.AreEqual(0, started);
        }

        [Test]
        public void NamedArena_DoesNotStartByPlayerEntering()
        {
            // 入口の判定でも始まると、イベントの開始と二重になる。名前付きはイベントからだけ始める。
            var player = new GameObject("Player") { tag = "Player" };
            try
            {
                _encounterGo.GetComponent<BoxCollider>().size = new Vector3(20f, 20f, 20f);
                TestReflection.SetField(_encounter, "_playerInside", player.transform);

                TestReflection.Invoke(_encounter, "Update");

                Assert.IsFalse(_encounter.HasStarted);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void UnnamedArena_StartsByPlayerEnteringDeepEnough()
        {
            // 名前を付けない通常の戦闘エリアは、これまで通り入口の判定で始まる。
            var player = new GameObject("Player") { tag = "Player" };
            try
            {
                TestReflection.SetField(_encounter, "_encounterId", "");
                _encounterGo.GetComponent<BoxCollider>().size = new Vector3(20f, 20f, 20f);
                TestReflection.SetField(_encounter, "_playerInside", player.transform);

                TestReflection.Invoke(_encounter, "Update");

                Assert.IsTrue(_encounter.HasStarted);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Defeated_AfterStartingFromDialogue_ReturnsToDialogue()
        {
            _gmm.TryStartDialogue(); // 会話イベントの battle の手順から始めた
            _encounter.StartFromEvent();
            var modes = new System.Collections.Generic.List<GameMode>();
            _gmm.Changed += () => modes.Add(_gmm.Mode);

            DefeatEnemy();

            Assert.AreEqual(GameMode.Dialogue, _gmm.Mode);
            CollectionAssert.DoesNotContain(
                modes,
                GameMode.Field,
                "会話の途中でフィールドを挟まない"
            );
        }

        [Test]
        public void Defeated_AfterStartingFromField_ReturnsToField()
        {
            _encounter.StartFromEvent();

            DefeatEnemy();

            Assert.AreEqual(GameMode.Field, _gmm.Mode);
        }

        // EnemyStatus.Die と同じく、倒れた印を立ててから知らせる(Die は Destroy まで呼ぶので直接は呼ばない)。
        private void DefeatEnemy()
        {
            var status = _enemy.GetComponent<EnemyStatus>();
            TestReflection.SetField(status, "<IsDead>k__BackingField", true);
            TestReflection.Invoke(_encounter, "OnEnemyDefeated");
        }

        [Test]
        public void FightFromEvent_WaitsUntilDefeated()
        {
            var fight = BattleEncounter.FightFromEvent("test_encounter");

            Assert.IsTrue(fight.MoveNext(), "戦闘中は待つ");
            Assert.IsTrue(_encounter.HasStarted);
            Assert.IsTrue(fight.MoveNext());

            DefeatEnemy();

            Assert.IsFalse(fight.MoveNext(), "決着したら次の手順へ進める");
        }

        [Test]
        public void FightFromEvent_UnknownName_SkipsWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no_such_encounter"));

            Assert.IsFalse(BattleEncounter.FightFromEvent("no_such_encounter").MoveNext());
        }
    }
}
