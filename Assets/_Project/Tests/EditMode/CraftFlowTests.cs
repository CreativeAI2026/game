using System.Collections;
using CreativeAI.UI.Common;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    public class CraftFlowTests
    {
        private GameObject _root;
        private GameObject _loadingRoot;
        private CraftPanelController _controller;
        private CraftResultPanelView _resultView;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CraftFlowTestRoot");

            _loadingRoot = new GameObject("Loading", typeof(CanvasGroup));
            _loadingRoot.transform.SetParent(_root.transform, false);
            var loadingView = _loadingRoot.AddComponent<CraftLoadingOverlayView>();
            TestReflection.SetField(loadingView, "_root", _loadingRoot);
            TestReflection.SetField(
                loadingView,
                "_canvasGroup",
                _loadingRoot.GetComponent<CanvasGroup>()
            );

            var resultRoot = new GameObject(
                "Result",
                typeof(CanvasGroup),
                typeof(CloseOnSelfClick),
                typeof(CraftResultPanelView)
            );
            resultRoot.transform.SetParent(_root.transform, false);
            _resultView = resultRoot.GetComponent<CraftResultPanelView>();
            TestReflection.SetField(
                _resultView,
                "_canvasGroup",
                resultRoot.GetComponent<CanvasGroup>()
            );
            TestReflection.SetField(
                _resultView,
                "_closeOnSelfClick",
                resultRoot.GetComponent<CloseOnSelfClick>()
            );

            var warningRoot = new GameObject("Warning", typeof(CanvasGroup));
            warningRoot.transform.SetParent(_root.transform, false);
            var warningView = warningRoot.AddComponent<CraftWarningToastView>();

            var controllerRoot = new GameObject("CraftPanelController");
            controllerRoot.transform.SetParent(_root.transform, false);
            _controller = controllerRoot.AddComponent<CraftPanelController>();

            var closeButtonRoot = new GameObject(
                "CloseButton",
                typeof(RectTransform),
                typeof(Button)
            );
            closeButtonRoot.transform.SetParent(controllerRoot.transform, false);

            TestReflection.SetField(_controller, "_loadingOverlayView", loadingView);
            TestReflection.SetField(_controller, "_resultPanelView", _resultView);
            TestReflection.SetField(_controller, "_warningToastView", warningView);
            TestReflection.SetField(
                _controller,
                "_closeButton",
                closeButtonRoot.GetComponent<Button>()
            );
            TestReflection.SetField(_controller, "_craftFlowDurationSeconds", 0f);

            _controller.CancelCraftFlow();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void RunCraftFlow_Success_RunsAfterLoadingAndWaitsForResultClose()
        {
            bool crafted = false;
            var routine = _controller.RunCraftFlow(
                () =>
                {
                    crafted = true;
                    return true;
                },
                null,
                1,
                null
            );

            Assert.IsTrue(routine.MoveNext(), "最初にローディング待機へ入る");
            Assert.IsTrue(_loadingRoot.activeSelf);
            Assert.IsFalse(crafted, "待機が終わるまではクラフト処理を実行しない");

            Assert.IsFalse(routine.MoveNext());
            Assert.IsTrue(crafted);
            Assert.IsTrue(_controller.IsCraftFlowRunning, "結果を閉じるまでは操作をロックする");

            _controller.CancelCraftFlow();
            Assert.IsFalse(_controller.IsCraftFlowRunning);
        }

        [Test]
        public void RunCraftFlow_Failure_HidesFlowAndUnlocksInteraction()
        {
            bool failed = false;
            var routine = _controller.RunCraftFlow(() => false, null, 1, null, () => failed = true);

            Assert.IsTrue(routine.MoveNext());
            Assert.IsFalse(routine.MoveNext());

            Assert.IsTrue(failed);
            Assert.IsFalse(_loadingRoot.activeSelf);
            Assert.IsFalse(_controller.IsCraftFlowRunning);
        }

        [Test]
        public void RunCraftFlow_WhileRunning_RejectsSecondFlow()
        {
            IEnumerator first = _controller.RunCraftFlow(() => true, null, 1, null);
            IEnumerator second = _controller.RunCraftFlow(() => true, null, 1, null);

            Assert.IsTrue(first.MoveNext());
            Assert.IsFalse(second.MoveNext());

            _controller.CancelCraftFlow();
        }

        [Test]
        public void RunCraftFlow_NullAction_DoesNotStart()
        {
            IEnumerator routine = _controller.RunCraftFlow(null, null, 1, null);

            Assert.IsFalse(routine.MoveNext());
            Assert.IsFalse(_controller.IsCraftFlowRunning);
        }

        [Test]
        public void RunCraftFlow_FirstCraft_ShowsNewBadgeUntilResultCloses()
        {
            IEnumerator routine = _controller.RunCraftFlow(
                () => true,
                null,
                1,
                null,
                showNewBadge: true
            );

            Assert.IsTrue(routine.MoveNext());
            Assert.IsFalse(routine.MoveNext());

            var badge = TestReflection.GetField<TMPro.TMP_Text>(_resultView, "_newBadge");
            Assert.IsNotNull(badge);
            Assert.IsTrue(badge.gameObject.activeSelf);

            _controller.CancelCraftFlow();
            Assert.IsFalse(badge.gameObject.activeSelf);
        }
    }
}
