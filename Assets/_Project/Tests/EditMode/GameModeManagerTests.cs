using System.Collections.Generic;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ゲームモード(フィールド / 戦闘)の遷移と変化通知の検証。
    /// </summary>
    public class GameModeManagerTests
    {
        private GameObject _go;
        private GameModeManager _gmm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(GameModeManager));
            _gmm = _go.AddComponent<GameModeManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void DefaultMode_IsField()
        {
            Assert.AreEqual(GameMode.Field, _gmm.CurrentMode);
        }

        [Test]
        public void EnterThenExitBattle_TogglesMode_AndNotifiesEachChange()
        {
            var changes = new List<GameMode>();
            _gmm.OnModeChanged += m => changes.Add(m);

            _gmm.EnterBattle();
            _gmm.ExitBattle();

            Assert.AreEqual(GameMode.Field, _gmm.CurrentMode);
            CollectionAssert.AreEqual(new[] { GameMode.Battle, GameMode.Field }, changes);
        }

        [Test]
        public void EnterBattle_WhenAlreadyBattle_DoesNotNotifyAgain()
        {
            _gmm.EnterBattle();

            int calls = 0;
            _gmm.OnModeChanged += _ => calls++;
            _gmm.EnterBattle();

            Assert.AreEqual(GameMode.Battle, _gmm.CurrentMode);
            Assert.AreEqual(0, calls);
        }
    }
}
