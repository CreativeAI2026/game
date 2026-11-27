using System.Collections;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話イベントの戦闘で、プレイヤーが倒れたときにタイトルへ戻すことの検証。
    /// </summary>
    public sealed class BattleRunnerTests
    {
        private readonly TestResidents _residents = new();
        private ITitleReturner _previousReturner;
        private FakeTitleReturner _returner;
        private GameObject _player;
        private PlayerStatus _status;
        private PlayerParameterData _parameters;
        private GameObject _enemy;
        private GameObject _arenaGo;
        private BattleArena _arena;

        [SetUp]
        public void SetUp()
        {
            _previousReturner = TitleReturnService.Current;
            _returner = new FakeTitleReturner();
            TitleReturnService.Current = _returner;

            _residents.Add<GameModeManager>();
            _player = TestPlayer.Create();
            if (!_player.TryGetComponent(out _status))
                _status = _player.AddComponent<PlayerStatus>();
            // HP を書き換えるには素のステータスが要る(Prefab なしで作るのでここで渡す)。
            _parameters = ScriptableObject.CreateInstance<PlayerParameterData>();
            TestReflection.SetField(_status, "_playerData", _parameters);

            _enemy = new GameObject("boss");
            _enemy.AddComponent<EnemyStatus>();

            _arenaGo = new GameObject("arena");
            _arenaGo.AddComponent<BoxCollider>().isTrigger = true;
            _arena = _arenaGo.AddComponent<BattleArena>();
            TestReflection.SetField(_arena, "_enemies", new[] { _enemy });
            TestReflection.SetField(_arena, "_arenaId", "test_arena");
            // EditMode では Awake が走らないので、設定を入れてから呼ぶ。
            TestReflection.Invoke(_arena, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            DestroyArena();
            Object.DestroyImmediate(_enemy);
            TestPlayer.Destroy(_player);
            Object.DestroyImmediate(_parameters);
            _residents.Clear();
            TitleReturnService.Current = _previousReturner;
        }

        [Test]
        public void Run_WhilePlayerIsAlive_KeepsWaiting()
        {
            IEnumerator run = new BattleRunner().Run("test_arena");

            Assert.IsTrue(run.MoveNext());
            Assert.IsTrue(run.MoveNext());
            Assert.AreEqual(0, _returner.Calls);
        }

        [Test]
        public void Run_WhenPlayerDies_ReturnsToTitleOnceAfterADelay()
        {
            IEnumerator run = new BattleRunner().Run("test_arena");
            Assert.IsTrue(run.MoveNext());

            _status.RestoreHp(0f);

            Assert.IsTrue(run.MoveNext());
            Assert.IsInstanceOf<WaitForSeconds>(run.Current, "倒れる動きを見せてから戻る");
            Assert.AreEqual(0, _returner.Calls);

            Assert.IsTrue(run.MoveNext());
            Assert.IsTrue(run.MoveNext(), "シーンが切り替わるまで、イベントを先へ進めない");
            Assert.AreEqual(1, _returner.Calls);

            // タイトルを読み込むと戦闘エリアが破棄され、待ちを抜ける。
            DestroyArena();
            Assert.IsFalse(run.MoveNext());
        }

        private void DestroyArena()
        {
            if (_arena == null)
                return;
            TestReflection.Invoke(_arena, "OnDestroy");
            Object.DestroyImmediate(_arenaGo);
        }

        private sealed class FakeTitleReturner : ITitleReturner
        {
            public int Calls { get; private set; }

            public void ReturnToTitle() => Calls++;
        }
    }
}
