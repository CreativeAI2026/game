using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合の本体(documents/Specification.md §2.3, §2.3.1)の検証。
    /// レシピ引き → カテゴリ検証 → 素材消費と結果付与(原子的) → 装備品はロール個体 / 食材は固定。
    /// MonoBehaviour を挟まない純粋サービスなので InventoryService を直接組んで叩く。
    /// </summary>
    public class RecipeCraftingServiceTests
    {
        private InventoryService _inv;
        private RecipeCraftingService _craft;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _inv = new InventoryService(new InventoryStorage());
            _craft = new RecipeCraftingService(_inv);
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

        private CraftRecipeData MakeRecipe(ItemData m1, ItemData m2, ItemData result)
        {
            var r = ScriptableObject.CreateInstance<CraftRecipeData>();
            r.material1 = m1;
            r.material2 = m2;
            r.resultItem = result;
            _assets.Add(r);
            return r;
        }

        private ItemStack StackOf(ItemData data) => _inv.GetAllItems().Find(s => s.Data == data);

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

            Assert.IsTrue(_craft.TryCraft(recipe, 1));

            Assert.AreEqual(0, CountOf(grapes), "素材は消費される");
            Assert.AreEqual(0, CountOf(miso));
            var result = StackOf(soup);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsFalse(
                result.IsInstance,
                "食材は固定ルールなので個体差ロールしない(spec §2.3)"
            );
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

            Assert.IsTrue(_craft.TryCraft(recipe, 1));

            var made = StackOf(result);
            Assert.IsNotNull(made);
            Assert.IsTrue(made.IsInstance, "装備品は端末でロールした個体になる(spec §2.3)");
            Assert.IsNotNull(made.RolledStats);
            Assert.LessOrEqual(made.RolledStats.Count, 2, "付与数は最大2つ(spec §2.1)");
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

            Assert.IsTrue(_craft.TryCraft(recipe, StackOf(a), StackOf(b)));

            Assert.AreEqual(2, CountOf(a));
            Assert.AreEqual(1, CountOf(b));
            Assert.AreEqual(1, CountOf(result));
        }

        [Test]
        public void GetMaximumCraftable_IsLimitedByScarcestMaterial()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3103));
            _inv.AddItem(a, 5);
            _inv.AddItem(b, 2);

            Assert.AreEqual(2, _craft.GetMaximumCraftable(recipe));
        }

        // --- カテゴリ検証(spec §2.3: 装備品同士 / 食材同士のみ) ---

        [Test]
        public void TryCraft_CrossCategory_IsRejected()
        {
            var food = Make<FoodData>(3001);
            var gear = Make<EquipmentData>(2001);
            var recipe = MakeRecipe(food, gear, Make<FoodData>(3104));
            _inv.AddItem(food, 1);
            _inv.AddItem(gear, 1);

            Assert.IsFalse(_craft.CanCraft(recipe));
            Assert.IsFalse(_craft.TryCraft(recipe, 1));
            Assert.AreEqual(1, CountOf(food), "失敗時は素材を減らさない");
            Assert.AreEqual(1, CountOf(gear));
        }

        [Test]
        public void TryCraft_WeaponMaterial_IsRejected()
        {
            // 武器は調合不可(spec §2.3)。
            var w1 = Make<WeaponData>(1001);
            var w2 = Make<WeaponData>(1002);
            var recipe = MakeRecipe(w1, w2, Make<EquipmentData>(2101));
            _inv.AddItem(w1, 1);
            _inv.AddItem(w2, 1);

            Assert.IsFalse(_craft.TryCraft(recipe, 1));
            Assert.AreEqual(1, CountOf(w1));
        }

        [Test]
        public void TryCraft_ImportantItemMaterial_IsRejected()
        {
            // 大事なものは調合の対象外(spec §2.1 補足)。
            var k1 = MakeImportant(4001);
            var k2 = MakeImportant(4002);
            var recipe = MakeRecipe(k1, k2, Make<FoodData>(3105));
            _inv.AddItem(k1, 1);
            _inv.AddItem(k2, 1);

            Assert.IsFalse(_craft.TryCraft(recipe, 1));
            Assert.AreEqual(1, CountOf(k1));
        }

        [Test]
        public void TryCraft_SameMaterialTwice_IsRejected()
        {
            var a = Make<FoodData>(3001);
            var recipe = MakeRecipe(a, a, Make<FoodData>(3106));
            _inv.AddItem(a, 5);

            Assert.IsFalse(_craft.TryCraft(recipe, 1));
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

            Assert.IsFalse(_craft.TryCraft(recipe, 1), "装備中の装備品は素材にできない");
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

            Assert.IsFalse(
                _craft.TryCraft(recipe, 1),
                "即時使用にセット済みの食材は素材にできない"
            );
            Assert.AreEqual(1, StackOf(a).Count);
        }

        // --- 原子性(spec §2.3.1: 素材を消費し結果を付与、を1回で確定) ---

        [Test]
        public void TryCraft_InsufficientMaterial_LeavesInventoryUntouched()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3108);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 3);
            _inv.AddItem(b, 1); // b が足りない

            Assert.IsFalse(_craft.CanCraft(recipe, 2));
            Assert.IsFalse(_craft.TryCraft(recipe, 2));

            Assert.AreEqual(3, CountOf(a), "片方だけ消える半端な状態にならない");
            Assert.AreEqual(1, CountOf(b));
            Assert.AreEqual(0, CountOf(result));
        }

        [Test]
        public void TryCraft_NullRecipeOrZeroQuantity_IsRejected()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var recipe = MakeRecipe(a, b, Make<FoodData>(3109));
            _inv.AddItem(a, 1);
            _inv.AddItem(b, 1);

            Assert.IsFalse(_craft.TryCraft(null, 1));
            Assert.IsFalse(_craft.TryCraft(recipe, 0));
            Assert.IsFalse(_craft.TryCraft(recipe, -1));
            Assert.AreEqual(2, _inv.GetAllItems().Count);
        }

        [Test]
        public void TryCraft_MultipleQuantity_ConsumesAndGrantsThatMany()
        {
            var a = Make<FoodData>(3001);
            var b = Make<FoodData>(3002);
            var result = Make<FoodData>(3110);
            var recipe = MakeRecipe(a, b, result);
            _inv.AddItem(a, 3);
            _inv.AddItem(b, 3);

            Assert.IsTrue(_craft.TryCraft(recipe, 2));

            Assert.AreEqual(1, CountOf(a));
            Assert.AreEqual(1, CountOf(b));
            Assert.AreEqual(2, StackOf(result).Count);
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
