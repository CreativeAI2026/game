using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合UIの確定フロー(documents/Specification.md §2.3.1)。
    /// 「実行した瞬間に確定。キャンセルもプレビューも無く、結果は<b>事後表示のみ</b>」。
    /// = 結果パネルは TryCraft が成功した後にしか出ず、失敗時は出ない。
    /// </summary>
    public class CraftFlowTests
    {
        private GameObject _invGo;
        private InventoryManager _inv;
        private GameObject _panelGo;
        private RecipeCraftPanel _panel;
        private GameObject _craftPanelGo;
        private SpyCraftPanel _craftPanel;
        private readonly List<Object> _assets = new();

        /// <summary>結果パネルの呼ばれ方だけを記録する差し替え。表示演出そのものは対象外。</summary>
        private sealed class SpyCraftPanel : CraftPanel
        {
            public int LoadingShown;
            public int ResultShown;
            public int WarningShown;
            public ItemData LastResultItem;
            public int LastResultCount;

            public override void ShowLoading() => LoadingShown++;

            public override void HideLoading() { }

            public override void HideLoadingAndResult() { }

            public override void HideWarning() { }

            public override void ShowMissingMaterialsWarning() => WarningShown++;

            public override void ShowResult(
                ItemData resultItem,
                int count,
                System.Action closeAction
            )
            {
                ResultShown++;
                LastResultItem = resultItem;
                LastResultCount = count;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("INV");
            _inv = _invGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inv);

            _craftPanelGo = new GameObject("CraftPanel");
            _craftPanel = _craftPanelGo.AddComponent<SpyCraftPanel>();

            _panelGo = new GameObject("RecipeCraftPanel");
            _panel = _panelGo.AddComponent<RecipeCraftPanel>();
            TestReflection.SetField(_panel, "_craftPanel", _craftPanel);
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            Object.DestroyImmediate(_panelGo);
            Object.DestroyImmediate(_craftPanelGo);
            Object.DestroyImmediate(_invGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private T Make<T>(int id)
            where T : ItemData
        {
            var a = ScriptableObject.CreateInstance<T>();
            a.id = id;
            _assets.Add(a);
            return a;
        }

        private CraftRecipeData MakeRecipe(ItemData m1, ItemData m2, ItemData result)
        {
            var r = ScriptableObject.CreateInstance<CraftRecipeData>();
            r.material1 = m1;
            r.material2 = m2;
            r.resultItem = result;
            _assets.Add(r);
            return r;
        }

        /// <summary>CraftRoutine を最後まで回す(WaitForSecondsRealtime は素通しでよい)。</summary>
        private void DriveCraft(CraftRecipeData recipe, int quantity)
        {
            var routine = (IEnumerator)
                TestReflection.Invoke(_panel, "CraftRoutine", recipe, quantity);
            while (routine.MoveNext()) { }
        }

        private int CountOf(ItemData data)
        {
            int total = 0;
            foreach (var s in _inv.GetAllItems())
                if (s.Data == data)
                    total += s.Count;
            return total;
        }

        [Test]
        public void CraftRoutine_Success_ConsumesThenShowsResultAfterwards()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3101);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);
            TestReflection.SetField(_panel, "_craftedRecipeForResult", recipe);
            TestReflection.SetField(_panel, "_craftedQuantityForResult", 1);

            DriveCraft(recipe, 1);

            Assert.AreEqual(0, CountOf(a), "素材は消費される");
            Assert.AreEqual(1, CountOf(result), "結果が付与される");
            Assert.AreEqual(1, _craftPanel.ResultShown, "結果は実行後に1回だけ出す(事後表示)");
            Assert.AreSame(result, _craftPanel.LastResultItem);
        }

        [Test]
        public void CraftRoutine_Failure_ShowsNoResult()
        {
            // 素材が足りないケース。プレビューが無い以上、失敗時は結果パネルを出してはいけない。
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3102));
            _inv.AddItem(a, 1); // b が無い
            TestReflection.SetField(_panel, "_craftedRecipeForResult", recipe);
            TestReflection.SetField(_panel, "_craftedQuantityForResult", 1);

            DriveCraft(recipe, 1);

            Assert.AreEqual(0, _craftPanel.ResultShown);
            Assert.AreEqual(1, CountOf(a), "失敗時に素材を減らさない");
        }

        [Test]
        public void CraftRoutine_ShowsLoadingBeforeTheResult()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3103);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);
            TestReflection.SetField(_panel, "_craftedRecipeForResult", recipe);
            TestReflection.SetField(_panel, "_craftedQuantityForResult", 1);

            var routine = (IEnumerator)TestReflection.Invoke(_panel, "CraftRoutine", recipe, 1);
            routine.MoveNext(); // ローディング表示 → 待ちに入る

            Assert.AreEqual(1, _craftPanel.LoadingShown);
            Assert.AreEqual(0, _craftPanel.ResultShown, "待っている間はまだ結果を見せない");

            while (routine.MoveNext()) { }
            Assert.AreEqual(1, _craftPanel.ResultShown);
        }

        [Test]
        public void CraftRoutine_NullRecipe_ShowsNoResult()
        {
            TestReflection.SetField(_panel, "_craftedRecipeForResult", (CraftRecipeData)null);
            TestReflection.SetField(_panel, "_craftedQuantityForResult", 1);

            DriveCraft(null, 1);

            Assert.AreEqual(0, _craftPanel.ResultShown);
        }

        [Test]
        public void CraftRoutine_ClearsCraftingFlagWhenDone()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3104));
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);
            TestReflection.SetField(_panel, "_craftedRecipeForResult", recipe);
            TestReflection.SetField(_panel, "_craftedQuantityForResult", 1);

            DriveCraft(recipe, 1);

            Assert.IsFalse(
                TestReflection.GetField<bool>(_panel, "_isCrafting"),
                "実行が終われば連打ガードは解除される"
            );
        }
    }
}
