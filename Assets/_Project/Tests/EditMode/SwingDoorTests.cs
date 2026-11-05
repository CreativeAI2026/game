using CreativeAI.Core;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 扉の開閉の検証(近づいたときだけ操作でき、動くのは扉板だけ)。
    /// 「移動中(Field)以外・会話イベント中は受け付けない」
    /// (拾得・イベント発火と同じ判断軸)。開くのは扉板だけで、ケーシングは動かない。
    /// </summary>
    public class SwingDoorTests
    {
        private readonly TestResidents _residents = new();
        private GameModeManager _gmm;
        private GameObject _doorGo;
        private GameObject _leafGo;
        private SwingDoor _door;
        private DoorInteractor _interactor;

        [SetUp]
        public void SetUp()
        {
            _gmm = _residents.Add<GameModeManager>();

            _doorGo = new GameObject("Door", typeof(SphereCollider));
            _leafGo = new GameObject(SwingDoor.DefaultLeafName);
            _leafGo.transform.SetParent(_doorGo.transform, false);
            // 幅 1.4u・厚さ 0.1u の扉板。原点は下辺の中央(glb と同じ)
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.transform.SetParent(_leafGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, 1f, 0f);
            panel.transform.localScale = new Vector3(1.4f, 2f, 0.1f);

            _door = _doorGo.AddComponent<SwingDoor>();
            TestReflection.SetField(_door, "_leaf", _leafGo.transform);
            TestReflection.SetField(_door, "_duration", 0.5f);
            TestReflection.Invoke(_door, "Awake");

            _interactor = _doorGo.AddComponent<DoorInteractor>();
            TestReflection.SetField(_interactor, "_door", _door);
        }

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            Object.DestroyImmediate(_doorGo);
        }

        private void PlayerEnters() => TestReflection.SetField(_interactor, "_playerInside", true);

        [Test]
        public void Open_SwingsTheLeafIntoTheRoom_AndCloseBringsItBack()
        {
            _door.Open();
            _door.Step(1f); // duration を超えて回せば開き切る

            var leaf = _leafGo.transform;
            Assert.AreEqual(90f, Quaternion.Angle(Quaternion.identity, leaf.localRotation), 0.01f);
            Assert.Less(leaf.localPosition.z, -0.5f, "室内側(-Z)へ押し開ける");
            Assert.AreEqual(0.65f, leaf.localPosition.x, 0.001f, "+X 側の蝶番まわりに回る");
            Assert.IsTrue(_door.IsOpen);

            _door.Close();
            _door.Step(1f);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, leaf.localRotation), 0.01f);
            Assert.Less(leaf.localPosition.magnitude, 0.001f, "閉めたら元の位置に戻る");
        }

        [Test]
        public void Step_MovesGradually_NotInstantly()
        {
            _door.Open();
            _door.Step(0.1f); // duration 0.5 の 1/5

            float angle = Quaternion.Angle(Quaternion.identity, _leafGo.transform.localRotation);
            Assert.Greater(angle, 0f, "回り始めている");
            Assert.Less(angle, 90f, "1フレームで開き切らない");
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
