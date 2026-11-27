using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ゲームモードの状態遷移(探索・会話・戦闘とメニュー)の可否・戻り先・変化通知の検証。
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
        public void Initially_FieldWithMenuClosed()
        {
            Assert.AreEqual(GameMode.Field, _gmm.Mode);
            Assert.IsFalse(_gmm.IsMenuOpen);
            Assert.IsTrue(_gmm.CanPlayerAct);
        }

        [Test]
        public void Dialogue_StartsFromField_AndEndsBackToField()
        {
            Assert.IsTrue(_gmm.TryStartDialogue());
            Assert.AreEqual(GameMode.Dialogue, _gmm.Mode);

            _gmm.EndDialogue();

            Assert.AreEqual(GameMode.Field, _gmm.Mode);
        }

        [Test]
        public void Dialogue_CannotStart_WhileMenuIsOpenOrInBattle()
        {
            _gmm.TryOpenMenu();
            Assert.IsFalse(_gmm.TryStartDialogue(), "メニュー中は会話を始めない");

            _gmm.CloseMenu();
            _gmm.TryStartBattle();
            Assert.IsFalse(_gmm.TryStartDialogue(), "戦闘中は会話を始めない");
            Assert.AreEqual(GameMode.Battle, _gmm.Mode);
        }

        [Test]
        public void Battle_FromField_ReturnsToField()
        {
            Assert.IsTrue(_gmm.TryStartBattle());

            _gmm.EndBattle();

            Assert.AreEqual(GameMode.Field, _gmm.Mode);
        }

        [Test]
        public void Battle_FromDialogue_ReturnsToDialogue_WithoutPassingField()
        {
            _gmm.TryStartDialogue();
            Assert.IsTrue(_gmm.TryStartBattle());
            var modes = new List<GameMode>();
            _gmm.Changed += () => modes.Add(_gmm.Mode);

            _gmm.EndBattle();

            CollectionAssert.AreEqual(
                new[] { GameMode.Dialogue },
                modes,
                "会話の途中で探索を挟まない"
            );
        }

        [Test]
        public void DialogueEndedDuringItsBattle_ReturnsToFieldAfterBattle()
        {
            _gmm.TryStartDialogue();
            _gmm.TryStartBattle();

            _gmm.EndDialogue(); // 戦闘の途中でイベントが打ち切られた

            Assert.AreEqual(GameMode.Battle, _gmm.Mode);
            _gmm.EndBattle();
            Assert.AreEqual(GameMode.Field, _gmm.Mode);
        }

        [Test]
        public void Menu_OpensOnlyInField_AndKeepsTheMode()
        {
            Assert.IsTrue(_gmm.TryOpenMenu());
            Assert.AreEqual(GameMode.Field, _gmm.Mode, "メニュー中も進行の状態は探索のまま");
            Assert.IsFalse(_gmm.CanPlayerAct);

            _gmm.CloseMenu();
            _gmm.TryStartDialogue();

            Assert.IsFalse(_gmm.TryOpenMenu(), "会話中はメニューを開けない");
            Assert.IsFalse(_gmm.IsMenuOpen);
        }

        [Test]
        public void Battle_WhileMenuIsOpen_StartsAndClosesMenu()
        {
            _gmm.TryOpenMenu();

            Assert.IsTrue(_gmm.TryStartBattle());
            Assert.AreEqual(GameMode.Battle, _gmm.Mode);
            Assert.IsFalse(_gmm.IsMenuOpen, "戦闘が始まったらメニューは閉じる");
        }

        [Test]
        public void ResetToField_ClosesMenuAndLeavesBattle()
        {
            _gmm.TryStartDialogue();
            _gmm.TryStartBattle();

            _gmm.ResetToField();

            Assert.AreEqual(GameMode.Field, _gmm.Mode);
            Assert.IsFalse(_gmm.IsMenuOpen);
        }

        [Test]
        public void RequestsThatChangeNothing_DoNotNotify()
        {
            _gmm.TryOpenMenu();
            int calls = 0;
            _gmm.Changed += () => calls++;

            _gmm.TryOpenMenu(); // 開いたまま切り替え
            _gmm.EndBattle(); // 戦闘していない

            Assert.AreEqual(0, calls);
        }
    }
}
