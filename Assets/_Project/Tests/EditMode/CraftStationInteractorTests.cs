using CreativeAI.Core;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 合成装置の「合成する」が、範囲内で移動中のときだけ出て、押すと合成画面を頼むことの検証。
    /// </summary>
    public class CraftStationInteractorTests
    {
        private GameObject _go;
        private CraftStationInteractor _interactor;
        private int _requests;

        private void CountRequest() => _requests++;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CraftStation", typeof(BoxCollider));
            _interactor = _go.AddComponent<CraftStationInteractor>();
            _requests = 0;
            CraftUiRequestService.Requested += CountRequest;
        }

        [TearDown]
        public void TearDown()
        {
            CraftUiRequestService.Requested -= CountRequest;
            InteractPromptService.Clear();
            EventPlaybackService.SetPlaying(false);
            Object.DestroyImmediate(_go);
        }

        private void PlayerEnters() => TestReflection.SetField(_interactor, "_playerInside", true);

        [Test]
        public void TryInteract_PlayerOutside_DoesNothing()
        {
            Assert.IsFalse(_interactor.TryInteract());
            Assert.AreEqual(0, _requests);
        }

        [Test]
        public void TryInteract_PlayerInside_RequestsCraftUiAndShowsPrompt()
        {
            PlayerEnters();

            Assert.IsTrue(_interactor.TryInteract());

            Assert.AreEqual(1, _requests);
            Assert.AreEqual("[E] 合成する", InteractPromptService.Label);
        }

        [Test]
        public void TryInteract_DuringEvent_DoesNothing()
        {
            PlayerEnters();
            EventPlaybackService.SetPlaying(true);

            Assert.IsFalse(_interactor.TryInteract(), "会話イベント中は開けない");
            Assert.AreEqual(0, _requests);
        }
    }
}
