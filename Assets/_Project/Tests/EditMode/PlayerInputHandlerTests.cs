using CreativeAI.Core;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話イベント中の操作不能の検証(入力を捨て、押しっぱなしも消す)。
    /// battle ステップ中は再生中でも戦えるよう入力を通す。
    /// </summary>
    public class PlayerInputHandlerTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _go;
        private PlayerInputHandler _input;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);
            _go = new GameObject("Player");
            _input = _go.AddComponent<PlayerInputHandler>();
            // EditMode では OnEnable が走らないので、購読を明示的に行う。
            TestReflection.Invoke(_input, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_input, "OnDisable");
            EventPlaybackService.SetPlaying(false);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_gmmGo);
        }

        [Test]
        public void EventStarts_ClearsHeldInputs()
        {
            _input.MoveInput(Vector2.up);
            _input.SprintInput(true);
            _input.AttackInput();

            EventPlaybackService.SetPlaying(true);

            Assert.AreEqual(Vector2.zero, _input.move);
            Assert.IsFalse(_input.sprint);
            Assert.IsFalse(_input.HasAttackInput, "解除直後に攻撃が暴発しない");
        }

        [Test]
        public void DuringEvent_IgnoresInputs()
        {
            EventPlaybackService.SetPlaying(true);

            _input.MoveInput(Vector2.up);
            _input.JumpInput(true);
            _input.AttackInput();
            _input.WeaponNextInput(true);

            Assert.AreEqual(Vector2.zero, _input.move);
            Assert.IsFalse(_input.jump);
            Assert.IsFalse(_input.HasAttackInput);
            Assert.IsFalse(_input.weaponNext);
        }

        [Test]
        public void AfterEvent_AcceptsInputsAgain()
        {
            EventPlaybackService.SetPlaying(true);
            EventPlaybackService.SetPlaying(false);

            _input.MoveInput(Vector2.up);
            _input.AttackInput();

            Assert.AreEqual(Vector2.up, _input.move);
            Assert.IsTrue(_input.HasAttackInput);
        }

        [Test]
        public void BattleStepDuringEvent_AcceptsInputs()
        {
            EventPlaybackService.SetPlaying(true);
            _gmm.EnterBattle();

            _input.MoveInput(Vector2.up);
            _input.AttackInput();

            Assert.AreEqual(Vector2.up, _input.move);
            Assert.IsTrue(_input.HasAttackInput);
        }

        [Test]
        public void BattleStepEnds_BackToConversation_ClearsHeldInputs()
        {
            EventPlaybackService.SetPlaying(true);
            _gmm.EnterBattle();
            _input.MoveInput(Vector2.up);

            _gmm.ExitBattle(); // 戦闘後に会話へ戻る

            Assert.AreEqual(Vector2.zero, _input.move);
        }
    }
}
