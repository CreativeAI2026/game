using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI;
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

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CraftFlowTestRoot");

            _loadingRoot = new GameObject("Loading", typeof(RectTransform));
            _loadingRoot.transform.SetParent(_root.transform, false);
            var gear = new GameObject("LoadingGear", typeof(RectTransform));
            gear.transform.SetParent(_loadingRoot.transform, false);

            var resultRoot = new GameObject(
                "Result",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(CloseOnSelfClick)
            );
            resultRoot.transform.SetParent(_root.transform, false);
            var itemName = CreateText("ItemName", resultRoot.transform);
            var itemParameters = CreateText("ItemParameters", resultRoot.transform);

            var warning = CreateText("WarningText", _root.transform);
            var warningCanvasGroup = warning.gameObject.AddComponent<CanvasGroup>();

            var closeButton = new GameObject("CloseButton", typeof(RectTransform), typeof(Button));
            closeButton.transform.SetParent(_root.transform, false);

            _controller = _root.AddComponent<CraftPanelController>();
            TestReflection.SetField(
                _controller,
                "_closeButton",
                closeButton.GetComponent<Button>()
            );
            TestReflection.SetField(_controller, "_loadingRoot", _loadingRoot);
            TestReflection.SetField(_controller, "_loadingGear", (RectTransform)gear.transform);
            TestReflection.SetField(
                _controller,
                "_resultCanvasGroup",
                resultRoot.GetComponent<CanvasGroup>()
            );
            TestReflection.SetField(
                _controller,
                "_resultCloseOnClick",
                resultRoot.GetComponent<CloseOnSelfClick>()
            );
            TestReflection.SetField(_controller, "_resultItemName", itemName);
            TestReflection.SetField(_controller, "_resultItemParameters", itemParameters);
            TestReflection.SetField(_controller, "_warningText", warning);
            TestReflection.SetField(_controller, "_warningCanvasGroup", warningCanvasGroup);
            TestReflection.SetField(_controller, "_craftFlowDurationSeconds", 0f);

            _controller.CancelCraftFlow();
        }

        private static TMP_Text CreateText(string name, Transform parent)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TextMeshProUGUI>();
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
                    return new ItemStack(null);
                },
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
            var routine = _controller.RunCraftFlow(() => null, null);

            Assert.IsTrue(routine.MoveNext());
            Assert.IsFalse(routine.MoveNext());

            Assert.IsFalse(_loadingRoot.activeSelf);
            Assert.IsFalse(_controller.IsCraftFlowRunning);
        }

        [Test]
        public void RunCraftFlow_WhileRunning_RejectsSecondFlow()
        {
            IEnumerator first = _controller.RunCraftFlow(() => new ItemStack(null), null);
            IEnumerator second = _controller.RunCraftFlow(() => new ItemStack(null), null);

            Assert.IsTrue(first.MoveNext());
            Assert.IsFalse(second.MoveNext());

            _controller.CancelCraftFlow();
        }

        [Test]
        public void RunCraftFlow_NullAction_DoesNotStart()
        {
            IEnumerator routine = _controller.RunCraftFlow(null, null);

            Assert.IsFalse(routine.MoveNext());
            Assert.IsFalse(_controller.IsCraftFlowRunning);
        }

        [Test]
        public void ShowResult_RolledEquipment_ShowsRolledParameters()
        {
            var equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.itemName = "Test Equipment";
            equipment.defense = 10; // 固定値はロール個体の表示に混ざらない
            var rolled = new ItemStack(
                equipment,
                new List<RolledStat> { new("AttackPct", 12.5f), new("CritRate", 4f) }
            );

            try
            {
                _controller.ShowResult(rolled, null);

                var parameters = TestReflection.GetField<TMP_Text>(
                    _controller,
                    "_resultItemParameters"
                );
                Assert.IsTrue(parameters.gameObject.activeSelf);
                StringAssert.Contains("攻撃 +12.5%", parameters.text);
                StringAssert.Contains("会心率 +4%", parameters.text);
                StringAssert.DoesNotContain("防御", parameters.text);
                Assert.AreEqual(2, parameters.text.Split('\n').Length);
            }
            finally
            {
                Object.DestroyImmediate(equipment);
            }
        }

        [Test]
        public void ShowResult_FixedEquipment_ShowsItemParameters()
        {
            var equipment = ScriptableObject.CreateInstance<EquipmentData>();
            equipment.itemName = "Test Equipment";
            equipment.defense = 10;
            equipment.criticalDamage = 10f;

            try
            {
                _controller.ShowResult(new ItemStack(equipment), null);

                var parameters = TestReflection.GetField<TMP_Text>(
                    _controller,
                    "_resultItemParameters"
                );
                StringAssert.Contains("防御 +10%", parameters.text);
                Assert.AreEqual(2, parameters.text.Split('\n').Length);
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
            var craftPanel = prefab.GetComponentInChildren<CraftPanelController>(true);
            Assert.IsNotNull(craftPanel);

            var resultRoot = TestReflection
                .GetField<CanvasGroup>(craftPanel, "_resultCanvasGroup")
                .transform;
            var parameters = TestReflection.GetField<TMP_Text>(craftPanel, "_resultItemParameters");
            Assert.IsNotNull(parameters, "ResultPanelのItemParameters参照が未設定です。");
            Assert.AreEqual("ItemParameters", parameters.gameObject.name);
            Assert.AreEqual(resultRoot, parameters.transform.parent.parent);
            Assert.IsFalse(parameters.raycastTarget);
        }
    }
}
