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
        private CraftPanel _controller;

        // 本番と同じ Prefab を読み、EditMode では走らない Awake を通す
        [SetUp]
        public void SetUp()
        {
            _root = PrefabUtility.LoadPrefabContents(
                "Assets/_Project/Features/UI/Crafting/Prefabs/CraftPanel.prefab"
            );
            _controller = _root.GetComponent<CraftPanel>();
            TestReflection.SetField(_controller, "_craftFlowDurationSeconds", 0f);
            TestReflection.Invoke(_controller, "Awake");
            _loadingRoot = TestReflection.GetField<GameObject>(_controller, "_loadingRoot");
            CancelCraft();
        }

        // 調合ボタンから先の流れ(ローディング → 作る → 結果 → 閉じる)を、作る処理を差し替えて直接動かす
        private IEnumerator CraftRoutine(System.Func<ItemStack> craft) =>
            (IEnumerator)TestReflection.Invoke(_controller, "CraftRoutine", craft);

        private void CancelCraft() => TestReflection.Invoke(_controller, "CancelCraft");

        [TearDown]
        public void TearDown()
        {
            CancelCraft();
            PrefabUtility.UnloadPrefabContents(_root);
        }

        [Test]
        public void CraftWarning_StaysCenteredWithoutMovingCraftSlots()
        {
            const string path = "Assets/_Project/Features/UI/Crafting/Prefabs/CraftPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                var controller = root.GetComponent<CraftPanel>();
                var warning = TestReflection.GetField<TMP_Text>(controller, "_warningText");
                var materials = TestReflection.GetField<MaterialSlot[]>(
                    controller,
                    "_materialSlots"
                );
                var crafted = TestReflection.GetField<SlotIconView>(controller, "_craftedItemSlot");
                var button = TestReflection.GetField<Button>(controller, "_craftButton");
                var targets = new Transform[]
                {
                    materials[0].transform,
                    materials[1].transform,
                    crafted.transform,
                    button.transform,
                };
                warning.gameObject.SetActive(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                var positions = System.Array.ConvertAll(targets, t => t.position);
                warning.text = "装備中のアイテムは素材にできません";
                warning.gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                for (int i = 0; i < targets.Length; i++)
                    Assert.That(
                        Vector3.Distance(positions[i], targets[i].position),
                        Is.LessThan(.01f),
                        targets[i].name
                    );
                Assert.AreSame(root.transform, warning.transform.parent);
                Assert.IsTrue(warning.GetComponent<LayoutElement>().ignoreLayout);
                Assert.That(
                    Vector3.Distance(
                        rect.TransformPoint(rect.rect.center),
                        warning.rectTransform.TransformPoint(warning.rectTransform.rect.center)
                    ),
                    Is.LessThan(.01f)
                );
                Assert.AreEqual(TextAlignmentOptions.Center, warning.alignment);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CraftRoutine_Success_RunsAfterLoadingAndWaitsForResultClose()
        {
            bool crafted = false;
            var routine = CraftRoutine(() =>
            {
                crafted = true;
                return new ItemStack(null);
            });

            Assert.IsTrue(routine.MoveNext(), "最初にローディング待機へ入る");
            Assert.IsTrue(_loadingRoot.activeSelf);
            Assert.IsFalse(crafted, "待機が終わるまではクラフト処理を実行しない");

            Assert.IsTrue(routine.MoveNext(), "結果を出したら閉じるまで待つ");
            Assert.IsTrue(crafted);
            Assert.IsTrue(_controller.IsCrafting, "結果を閉じるまでは操作をロックする");

            CancelCraft();
            Assert.IsFalse(_controller.IsCrafting);
        }

        [Test]
        public void CraftRoutine_Failure_HidesFlowAndUnlocksInteraction()
        {
            var routine = CraftRoutine(() => null);

            Assert.IsTrue(routine.MoveNext());
            Assert.IsFalse(routine.MoveNext());

            Assert.IsFalse(_loadingRoot.activeSelf);
            Assert.IsFalse(_controller.IsCrafting);
        }

        [Test]
        public void CraftRoutine_WhileCrafting_RejectsSecondCraft()
        {
            IEnumerator first = CraftRoutine(() => new ItemStack(null));
            IEnumerator second = CraftRoutine(() => new ItemStack(null));

            Assert.IsTrue(first.MoveNext());
            Assert.IsFalse(second.MoveNext());

            CancelCraft();
        }

        [Test]
        public void ShowResult_RolledEquipment_ShowsRolledParameters()
        {
            var equipment = ScriptableObject.CreateInstance<ItemData>();
            equipment.category = ItemCategory.Equipment;
            equipment.itemName = "Test Equipment";
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
        public void UIRootPrefab_CraftResultPanel_HasAttachedItemParametersText()
        {
            const string prefabPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.IsNotNull(prefab, prefabPath);
            var craftPanel = prefab.GetComponentInChildren<CraftPanel>(true);
            Assert.IsNotNull(craftPanel);

            var resultRoot = TestReflection
                .GetField<CanvasGroup>(craftPanel, "_resultCanvasGroup")
                .transform;
            var parameters = TestReflection.GetField<TMP_Text>(craftPanel, "_resultItemParameters");
            Assert.IsNotNull(parameters, "ResultPanelのItemParameters参照が未設定です。");
            Assert.AreEqual("ItemParameters", parameters.gameObject.name);
            Assert.IsTrue(parameters.transform.IsChildOf(resultRoot));
            Assert.IsFalse(parameters.raycastTarget);
        }
    }
}
