using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.Interaction;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 扉の開閉の検証(近づいたときだけ操作でき、動くのは扉板だけ)。
    /// 「移動中(Field)以外・会話イベント中は受け付けない」(documents/MapLayout.md §1「扉付きの出入口」)
    /// (拾得・イベント発火と同じ判断軸)。開くのは扉板だけで、ケーシングは動かない。
    /// </summary>
    public class SlidingDoorTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _doorGo;
        private GameObject _leafGo;
        private SlidingDoor _door;
        private DoorInteractor _interactor;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);

            _doorGo = new GameObject("Door", typeof(SphereCollider));
            _leafGo = new GameObject(SlidingDoor.DefaultLeafName);
            _leafGo.transform.SetParent(_doorGo.transform, false);

            _door = _doorGo.AddComponent<SlidingDoor>();
            TestReflection.SetField(_door, "_leaf", _leafGo.transform);
            TestReflection.SetField(_door, "_slideDistance", 1.4f); // 実測に頼らず固定
            TestReflection.SetField(_door, "_duration", 0.5f);
            TestReflection.Invoke(_door, "Awake");

            _interactor = _doorGo.AddComponent<DoorInteractor>();
            TestReflection.SetField(_interactor, "_door", _door);
        }

        [TearDown]
        public void TearDown()
        {
            EventPlaybackService.SetPlaying(false);
            InteractPromptService.Clear();
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_doorGo);
            Object.DestroyImmediate(_gmmGo);
        }

        private void PlayerEnters() => TestReflection.SetField(_interactor, "_playerInside", true);

        [Test]
        public void Open_SlidesTheLeafAside_AndCloseBringsItBack()
        {
            _door.Open();
            _door.Step(1f); // duration を超えて回せば開き切る

            Assert.AreEqual(
                1.4f,
                _leafGo.transform.localPosition.x,
                0.001f,
                "扉板が横に引き込まれる"
            );
            Assert.IsTrue(_door.IsOpen);

            _door.Close();
            _door.Step(1f);
            Assert.AreEqual(
                0f,
                _leafGo.transform.localPosition.x,
                0.001f,
                "閉めたら元の位置に戻る"
            );
        }

        [Test]
        public void Step_MovesGradually_NotInstantly()
        {
            _door.Open();
            _door.Step(0.1f); // duration 0.5 の 1/5

            float x = _leafGo.transform.localPosition.x;
            Assert.Greater(x, 0f, "動き始めている");
            Assert.Less(x, 1.4f, "1フレームで開き切らない");
        }

        [Test]
        public void Interact_NeedsThePlayerNearby()
        {
            Assert.IsFalse(_interactor.TryInteract(), "離れていれば押しても開かない");
            Assert.IsFalse(_door.IsOpen);

            PlayerEnters();
            Assert.IsTrue(_interactor.TryInteract());
            Assert.IsTrue(_door.IsOpen);
        }

        [Test]
        public void Interact_IsBlocked_DuringBattleOrDialogue()
        {
            PlayerEnters();

            _gmm.EnterBattle();
            Assert.IsFalse(_interactor.TryInteract(), "戦闘中は開けない");

            _gmm.ExitBattle();
            EventPlaybackService.SetPlaying(true);
            Assert.IsFalse(_interactor.TryInteract(), "会話イベント中は開けない");

            EventPlaybackService.SetPlaying(false);
            Assert.IsTrue(_interactor.TryInteract(), "移動中に戻れば開ける");
        }

        [Test]
        public void Interact_ShowsThePromptForThisDoorOnly()
        {
            PlayerEnters();
            Assert.IsTrue(_interactor.TryInteract());
            StringAssert.Contains(
                "扉を閉じる",
                InteractPromptService.Label,
                "開いた後は閉じる案内に変わる"
            );

            // 別の扉が消そうとしても、出しているのは自分なので消えない
            InteractPromptService.Hide(new object());
            Assert.IsNotNull(InteractPromptService.Label);

            InteractPromptService.Hide(_interactor);
            Assert.IsNull(InteractPromptService.Label);
        }
    }
}
