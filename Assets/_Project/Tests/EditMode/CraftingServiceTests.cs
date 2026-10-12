using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合本体の検証(レシピ引き → カテゴリ検証 → 素材消費と結果付与を原子的に行う)。
    /// 装備品はロール個体 / 食材は固定。
    /// MonoBehaviour を挟まない純粋サービスなので InventoryService を直接組んで叩く。
    /// </summary>
    public class CraftingServiceTests
    {
        private InventoryService _inv;
        private CraftingService _craft;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _inv = new InventoryService();
            _craft = new CraftingService(_inv);
        }

        [TearDown]
        public void TearDown()
        {
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

        private ItemData MakeImportant(int id)
        {
            var a = Make<ItemData>(id);
            a.category = ItemCategory.Important;
            return a;
        }

        private CraftRecipe MakeRecipe(ItemData m1, ItemData m2, ItemData result)
        {
            var r = new CraftRecipe();
            r.material1 = m1;
            r.material2 = m2;
            r.resultItem = result;
            return r;
        }

        private ItemStack StackOf(ItemData data) => _inv.GetAllItems().Find(s => s.Data == data);

        private bool CanCraftOnce(CraftRecipe r) =>
            _craft.CanCraft(r, StackOf(r.material1), StackOf(r.material2));

        private bool CraftOnce(CraftRecipe r) =>
            _craft.TryCraft(r, StackOf(r.material1), StackOf(r.material2), out _);

        /// <summary>在庫にあるその品の総数(スタックごと消えていれば 0)。</summary>
        private int CountOf(ItemData data) =>
            _inv.GetAllItems().Where(s => s.Data == data).Sum(s => s.Count);

        // --- 正常系 ---

        [Test]
        public void TryCraft_FoodPair_ConsumesMaterials_AndGrantsStackedResult()
        {
            var grapes = Make<FoodData>(3002);
            var miso = Make<FoodData>(3010);
            var soup = Make<FoodData>(3101);
            var recipe = MakeRecipe(grapes, miso, soup);
            _inv.AddItem(grapes, 1);
            _inv.AddItem(miso, 1);

            Assert.IsTrue(_craft.TryCraft(recipe, StackOf(grapes), StackOf(miso), out var crafted));

            Assert.AreSame(StackOf(soup), crafted, "完成品が入った在庫のスタックを返す");
            Assert.AreEqual(0, CountOf(grapes), "素材は消費される");
            Assert.AreEqual(0, CountOf(miso));
            var result = StackOf(soup);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsFalse(result.IsInstance, "食材は固定ルールなので個体差ロールしない");
        }

        [Test]
        public void TryCraft_EquipmentPair_GrantsRolledInstance()
        {
            var a = Make<EquipmentData>(2001);
            a.attack = 10;
            var b = Make<EquipmentData>(2002);
            b.defense = 8;
            var result = Make<EquipmentData>(2101);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);

            Assert.IsTrue(_craft.TryCraft(recipe, StackOf(a), StackOf(b), out var crafted));

            var made = StackOf(result);
            Assert.IsNotNull(made);
            Assert.AreSame(made, crafted, "作った個体そのもの(結果表示に使う)を返す");
            Assert.IsTrue(made.IsInstance, "装備品は端末でロールした個体になる");
            Assert.IsNotNull(made.RolledStats);
            Assert.LessOrEqual(made.RolledStats.Count, 2, "付与数は最大2つ");
        }

        [Test]
        public void TryCraft_SelectedStacks_ConsumesExactlyOneEach()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3102);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 3);
            _inv.AddItem(b, 2);

            Assert.IsTrue(_craft.TryCraft(recipe, StackOf(a), StackOf(b), out _));

            Assert.AreEqual(2, CountOf(a));
            Assert.AreEqual(1, CountOf(b));
            Assert.AreEqual(1, CountOf(result));
        }

        // --- カテゴリ検証(装備品同士 / 食材同士のみ) ---

        [Test]
        public void TryCraft_CrossCategory_IsRejected()
        {
            var food = Make<FoodData>(3001);
            var gear = Make<EquipmentData>(2001);
            var recipe = MakeRecipe(food, gear, Make<FoodData>(3104));
            _inv.AddItem(food, 1);
            _inv.AddItem(gear, 1);

            Assert.IsFalse(CanCraftOnce(recipe));
            Assert.IsFalse(CraftOnce(recipe));
            Assert.AreEqual(1, CountOf(food), "失敗時は素材を減らさない");
            Assert.AreEqual(1, CountOf(gear));
        }

        [Test]
        public void TryCraft_WeaponMaterial_IsRejected()
        {
            // 武器は調合不可。
            var w1 = Make<WeaponData>(1001);
            var w2 = Make<WeaponData>(1002);
            var recipe = MakeRecipe(w1, w2, Make<EquipmentData>(2101));
            _inv.AddItem(w1, 1);
            _inv.AddItem(w2, 1);

            Assert.IsFalse(CraftOnce(recipe));
            Assert.AreEqual(1, CountOf(w1));
        }

        [Test]
        public void TryCraft_ImportantItemMaterial_IsRejected()
        {
            // 大事なものは調合の対象外。
            var k1 = MakeImportant(4001);
            var k2 = MakeImportant(4002);
            var recipe = MakeRecipe(k1, k2, Make<FoodData>(3105));
            _inv.AddItem(k1, 1);
            _inv.AddItem(k2, 1);

            Assert.IsFalse(CraftOnce(recipe));
            Assert.AreEqual(1, CountOf(k1));
        }

        [Test]
        public void TryCraft_SameMaterialTwice_IsRejected()
        {
            var a = Make<FoodData>(3001);
            var recipe = MakeRecipe(a, a, Make<FoodData>(3106));
            _inv.AddItem(a, 5);

            Assert.IsFalse(CraftOnce(recipe));
            Assert.AreEqual(5, CountOf(a));
        }

        // --- 素材として使えない状態 ---

        [Test]
        public void TryCraft_EquippedMaterial_IsNotConsumed()
        {
            var a = Make<EquipmentData>(2001);
            var b = Make<EquipmentData>(2002);
            var recipe = MakeRecipe(a, b, Make<EquipmentData>(2101));
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);
            StackOf(a).IsEquipped = true;

            Assert.IsFalse(CraftOnce(recipe), "装備中の装備品は素材にできない");
            Assert.AreEqual(1, CountOf(a));
            Assert.AreEqual(1, CountOf(b));
        }

        [Test]
        public void TryCraft_QuickFoodMaterial_IsNotConsumed()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3107));
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);
            Assert.IsTrue(_inv.SetQuickFood(0, StackOf(a)));

            Assert.IsFalse(CraftOnce(recipe), "即時使用にセット済みの食材は素材にできない");
            Assert.AreEqual(1, StackOf(a).Count);
        }

        // --- 原子性(素材を消費し結果を付与、を1回で確定) ---

        [Test]
        public void TryCraft_MissingMaterial_LeavesInventoryUntouched()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3108);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 3); // b を持っていない

            Assert.IsFalse(CanCraftOnce(recipe));
            Assert.IsFalse(CraftOnce(recipe));

            Assert.AreEqual(3, CountOf(a), "片方だけ消える半端な状態にならない");
            Assert.AreEqual(0, CountOf(result));
        }

        [Test]
        public void TryCraft_NullRecipe_IsRejected()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);

            Assert.IsFalse(_craft.TryCraft(null, StackOf(a), StackOf(b), out var crafted));
            Assert.IsNull(crafted);
            Assert.AreEqual(2, _inv.GetAllItems().Count);
        }

        [Test]
        public void MatchesMaterials_IsOrderIndependent()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3111));

            Assert.IsTrue(recipe.MatchesMaterials(a, b));
            Assert.IsTrue(recipe.MatchesMaterials(b, a), "素材の並び順は問わない");
            Assert.IsFalse(recipe.MatchesMaterials(a, a));
        }
    }
}
