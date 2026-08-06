using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// documents/Specification.md §2 のインベントリ仕様のうち、値・上限が仕様書に明記されているもの:
    /// 装備品は最大3つ / hasItem は大事なもの限定 / 食材の回復量は固定(合成前20% 合成後50%)。
    /// </summary>
    public class InventoryRulesTests
    {
        private GameObject _go;
        private InventoryManager _inv;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(InventoryManager));
            _inv = _go.AddComponent<InventoryManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            ItemDB.InjectForTests(null); // 注入を解除して実カタログに戻す
        }

        private static EquipmentData MakeEquipment(int id)
        {
            var d = ScriptableObject.CreateInstance<EquipmentData>(); // OnEnable が category=Equipment を設定
            d.id = id;
            d.attack = 1;
            return d;
        }

        // --- §2.1 装備品は最大3つ ---

        [Test]
        public void SetEquipped_RejectsFourthEquipment()
        {
            var stacks = new List<ItemStack>();
            for (int i = 0; i < 4; i++)
            {
                var gear = MakeEquipment(100 + i);
                _inv.AddItem(gear, 1);
                stacks.Add(_inv.GetAllItems().Find(s => s.Data == gear));
            }

            for (int i = 0; i < 3; i++)
                _inv.SetEquipped(stacks[i], true);

            // 4つ目は上限(MaxEquippedEquipment=3)で拒否される。警告は出るがエラーにはならない。
            _inv.SetEquipped(stacks[3], true);

            Assert.AreEqual(3, InventoryManager.MaxEquippedEquipment);
            Assert.IsFalse(stacks[3].IsEquipped, "4つ目の装備が上限を超えて装着された");
            Assert.AreEqual(3, _inv.GetAllItems().Count(s => s.IsEquipped));
        }

        [Test]
        public void SetEquipped_AfterUnequipping_AllowsAnotherEquipment()
        {
            var stacks = new List<ItemStack>();
            for (int i = 0; i < 4; i++)
            {
                var gear = MakeEquipment(200 + i);
                _inv.AddItem(gear, 1);
                stacks.Add(_inv.GetAllItems().Find(s => s.Data == gear));
            }
            for (int i = 0; i < 3; i++)
                _inv.SetEquipped(stacks[i], true);

            _inv.SetEquipped(stacks[0], false); // 1枠あける
            _inv.SetEquipped(stacks[3], true);

            Assert.IsTrue(stacks[3].IsEquipped);
            Assert.AreEqual(3, _inv.GetAllItems().Count(s => s.IsEquipped));
        }

        // --- §4.1 / ScenarioReference「hasItem の制約」: 大事なもの限定 ---

        [Test]
        public void HasImportantItem_TrueOnlyForOwnedKeyItem()
        {
            var keyItem = ScriptableObject.CreateInstance<ItemData>();
            keyItem.id = 900;
            keyItem.key = "mysterious_key";
            keyItem.category = ItemCategory.Important;
            ItemDB.InjectForTests(new[] { keyItem });

            Assert.IsFalse(_inv.HasImportantItem("mysterious_key"), "未所持なら false");

            _inv.AddItem(keyItem, 1);

            Assert.IsTrue(_inv.HasImportantItem("mysterious_key"));
        }

        [Test]
        public void HasImportantItem_FalseForEquipmentAndFood_EvenWhenOwned()
        {
            // 「装備品/食材の所持は条件にしない」(Specification.md §4.1)。所持していても false。
            var gear = MakeEquipment(901);
            gear.key = "umbrella";
            var food = ScriptableObject.CreateInstance<FoodData>(); // OnEnable が category=Food
            food.id = 902;
            food.key = "apple";
            ItemDB.InjectForTests(new ItemData[] { gear, food });

            _inv.AddItem(gear, 1);
            _inv.AddItem(food, 1);

            Assert.IsFalse(_inv.HasImportantItem("umbrella"), "装備品は hasItem の対象外");
            Assert.IsFalse(_inv.HasImportantItem("apple"), "食材は hasItem の対象外");
        }

        [Test]
        public void HasImportantItem_FalseForUnknownKey()
        {
            var keyItem = ScriptableObject.CreateInstance<ItemData>();
            keyItem.id = 903;
            keyItem.key = "card_key";
            keyItem.category = ItemCategory.Important;
            ItemDB.InjectForTests(new[] { keyItem });

            Assert.IsFalse(_inv.HasImportantItem("mysterious_ky"), "打ち間違いキーは false");
        }

        // --- §2.1 食材の回復量は固定(合成前20% / 合成後50%) ---

        [Test]
        public void FoodData_HealFraction_IsFixedByCraftedFlag()
        {
            var raw = ScriptableObject.CreateInstance<FoodData>();
            Assert.AreEqual(0.20f, raw.HealFraction, 1e-6f, "合成前の食材は最大HPの20%回復");
            Assert.IsFalse(raw.IsCraftedResult);

            var crafted = ScriptableObject.CreateInstance<FoodData>();
            var so = new UnityEditor.SerializedObject(crafted);
            so.FindProperty("_craftedResult").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(crafted.IsCraftedResult);
            Assert.AreEqual(0.50f, crafted.HealFraction, 1e-6f, "合成後の食材は最大HPの50%回復");
        }

        [Test]
        public void FoodData_HealFractionConstants_MatchSpec()
        {
            Assert.AreEqual(0.20f, FoodData.PreCraftHealFraction, 1e-6f);
            Assert.AreEqual(0.50f, FoodData.PostCraftHealFraction, 1e-6f);
        }
    }
}
