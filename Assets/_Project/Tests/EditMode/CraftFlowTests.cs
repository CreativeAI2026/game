using System.Collections;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合の実行フロー(ローディング → 結果表示 → クローズ)と多重実行の拒否の検証。
    /// </summary>
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
            var itemNameObject = new GameObject(
                "ItemName",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            itemNameObject.transform.SetParent(resultRoot.transform, false);
            TestReflection.SetField(
                _resultView,
                "_itemName",
                itemNameObject.GetComponent<TextMeshProUGUI>()
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

        [Test]
        public void ShowResult_Equipment_ShowsParametersBelowItemName()
        {
            var equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.itemName = "Test Equipment";
            equipment.defense = 10;
            equipment.criticalDamage = 10f;

            try
            {
                _controller.ShowResult(equipment, 1, null);

                var parameters = TestReflection.GetField<TMP_Text>(_resultView, "_itemParameters");
                Assert.IsNotNull(parameters);
                Assert.IsTrue(parameters.gameObject.activeSelf);
                StringAssert.Contains("+10%", parameters.text);
                Assert.AreEqual(2, parameters.text.Split('\n').Length);

                var itemName = TestReflection.GetField<TMP_Text>(_resultView, "_itemName");
                Assert.Less(
                    ((RectTransform)parameters.transform).anchoredPosition.y,
                    ((RectTransform)itemName.transform).anchoredPosition.y
                );
            }
            finally
            {
                Object.DestroyImmediate(equipment);
            }
        }

        [Test]
        public void UIRootPrefab_CraftResultPanel_HasAttachedItemParametersText()
        {
            const string prefabPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(prefab, prefabPath);
            var resultView = prefab.GetComponentInChildren<CraftResultPanelView>(true);
            Assert.IsNotNull(resultView);

            var parameters = TestReflection.GetField<TMP_Text>(resultView, "_itemParameters");
            Assert.IsNotNull(parameters, "ResultPanelのItemParameters参照が未設定です。");
            Assert.AreEqual("ItemParameters", parameters.gameObject.name);
            Assert.AreEqual(resultView.transform, parameters.transform.parent.parent);
            Assert.IsFalse(parameters.raycastTarget);
        }
    }
}
