using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話イベントから戦闘エリア(BattleArena)を名前で探して始める入口の検証。
    /// </summary>
    public class BattleArenaTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _enemy;
        private GameObject _arenaGo;
        private BattleArena _arena;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            // EditMode では Awake が自動で走らず、DontDestroyOnLoad も使えないので、Instance だけ直接入れる。
            TestReflection.SetStaticProperty<GameModeManager>("Instance", _gmm);

            _enemy = new GameObject("boss");
            _enemy.AddComponent<EnemyStatus>();

            _arenaGo = new GameObject("arena");
            _arenaGo.AddComponent<BoxCollider>().isTrigger = true;
            _arena = _arenaGo.AddComponent<BattleArena>();
            TestReflection.SetField(_arena, "_enemies", new[] { _enemy });
            TestReflection.SetField(_arena, "_arenaId", "test_arena");
            // Awake で名前を登録して敵を隠すので、設定を入れてから呼ぶ。
            TestReflection.Invoke(_arena, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode では OnDestroy も自動で走らない。名前の登録を残さないよう自分で呼ぶ。
            TestReflection.Invoke(_arena, "OnDestroy");
            Object.DestroyImmediate(_arenaGo);
            Object.DestroyImmediate(_enemy);
            Object.DestroyImmediate(_gmmGo);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
        }

        [Test]
        public void Find_ReturnsTheArenaWithThatName()
        {
            Assert.AreSame(_arena, BattleArena.Find("test_arena"));
        }

        [Test]
        public void Find_UnknownName_ReturnsNull()
        {
            Assert.IsNull(BattleArena.Find("no_such_arena"));
        }

        [Test]
        public void Find_AfterDestroy_ReturnsNull()
        {
            TestReflection.Invoke(_arena, "OnDestroy");

            Assert.IsNull(BattleArena.Find("test_arena"));
        }

        [Test]
        public void Awake_HidesTheEnemyUntilTheBattleStarts()
        {
            Assert.IsFalse(_enemy.activeSelf);
            Assert.IsFalse(_arena.HasStarted);
        }

        [Test]
        public void StartFromEvent_ShowsEnemy_EntersBattle_AndNotifies()
        {
            int started = 0;
            _arena.BattleStarted += () => started++;

            bool result = _arena.StartFromEvent();

            Assert.IsTrue(result);
            Assert.IsTrue(_enemy.activeSelf);
            Assert.IsTrue(_arena.HasStarted);
            Assert.AreEqual(GameMode.Battle, _gmm.CurrentMode);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void StartFromEvent_WhenAlreadyStarted_DoesNothing()
        {
            int started = 0;
            _arena.StartFromEvent();
            _arena.BattleStarted += () => started++;

            bool result = _arena.StartFromEvent();

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
                _arenaGo.GetComponent<BoxCollider>().size = new Vector3(20f, 20f, 20f);
                TestReflection.SetField(_arena, "_playerInside", player.transform);

                TestReflection.Invoke(_arena, "Update");

                Assert.IsFalse(_arena.HasStarted);
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
                TestReflection.SetField(_arena, "_arenaId", "");
                _arenaGo.GetComponent<BoxCollider>().size = new Vector3(20f, 20f, 20f);
                TestReflection.SetField(_arena, "_playerInside", player.transform);

                TestReflection.Invoke(_arena, "Update");

                Assert.IsTrue(_arena.HasStarted);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
