using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 食材使用の検証(HP即時回復をその場で適用し、在庫を1つ消費)。
    /// 回復量は最大HPに対する固定割合(合成前20%/合成後50%)。
    /// </summary>
    public class FoodUseTests
    {
        private readonly TestResidents _residents = new();
        private InventoryManager _inv;
        private EquipmentSlots _equipment;
        private GameObject _playerGo;
        private PlayerStatus _status;
        private PlayerParameterData _playerData;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _inv = _residents.Add<InventoryManager>();
            _equipment = _inv.Equipment;

            _playerData = ScriptableObject.CreateInstance<PlayerParameterData>();
            _playerData.baseMaxLife = 1000f;
            _playerData.baseAttackPower = 2000f;
            _playerData.baseDefense = 500f;
            _assets.Add(_playerData);

            _playerGo = new GameObject("Player");
            _status = _playerGo.AddComponent<PlayerStatus>();
            TestReflection.SetField(_status, "_playerData", _playerData);
        }

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            Object.DestroyImmediate(_playerGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private FoodData MakeFood(int id, bool crafted = false)
        {
            var f = ScriptableObject.CreateInstance<FoodData>();
            f.id = id;
            if (crafted)
                TestReflection.SetField(f, "_craftedResult", true);
            _assets.Add(f);
            return f;
        }

        private ItemStack StackOf(ItemData data) => _inv.GetAllItems().Find(s => s.Data == data);

        [Test]
        public void TryEat_PreCraftFood_HealsTwentyPercentOfMaxHp_AndConsumesOne()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 3);
            _status.RestoreHp(100f); // 最大1000 のうち 100

            Assert.IsTrue(FoodUse.TryEat(_status, StackOf(apple)));

            Assert.AreEqual(300f, _status.CurrentHp, 1e-2f, "最大HP1000 の 20% = 200 回復");
            Assert.AreEqual(2, StackOf(apple).Count, "在庫が1つ減る");
        }

        [Test]
        public void TryEat_PostCraftFood_HealsFiftyPercentOfMaxHp()
        {
            var soup = MakeFood(3101, crafted: true);
            _inv.AddItem(soup, 1);
            _status.RestoreHp(100f);

            Assert.IsTrue(FoodUse.TryEat(_status, StackOf(soup)));

            Assert.AreEqual(600f, _status.CurrentHp, 1e-2f, "最大HP1000 の 50% = 500 回復");
        }

        [Test]
        public void TryEat_DoesNotOverheal()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(950f);

            Assert.IsTrue(FoodUse.TryEat(_status, StackOf(apple)));

            Assert.AreEqual(1000f, _status.CurrentHp, 1e-2f, "最大HP を超えない");
        }

        [Test]
        public void TryEat_HealScalesWithBuffedMaxHp()
        {
            // 装備の最大HP% で最大HPが増えると、回復量(割合)も増える。
            var gear = ScriptableObject.CreateInstance<ItemData>();
            gear.category = ItemCategory.Equipment;
            _assets.Add(gear);
            _equipment.Set(
                0,
                _inv.AddInstance(gear, new[] { new RolledStat(nameof(StatType.MaxHpPct), 100f) })
            ); // 最大HP 2000
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(100f); // HP0 は死亡扱いで回復を受け付けないため、生存状態から開始

            Assert.IsTrue(FoodUse.TryEat(_status, StackOf(apple)));

            Assert.AreEqual(500f, _status.CurrentHp, 1e-2f, "最大HP2000 の 20% = 400 回復");
        }

        [Test]
        public void TryEat_NonFood_IsRejected()
        {
            var gear = ScriptableObject.CreateInstance<ItemData>();
            gear.category = ItemCategory.Equipment;
            gear.id = 2001;
            _assets.Add(gear);
            _inv.AddItem(gear, 1);
            _status.RestoreHp(100f);

            Assert.IsFalse(FoodUse.TryEat(_status, StackOf(gear)), "食材以外は使用できない");
            Assert.AreEqual(100f, _status.CurrentHp, 1e-2f);
            Assert.AreEqual(1, StackOf(gear).Count, "在庫も減らさない");
        }

        [Test]
        public void TryEat_NullOrForeignStack_IsRejected()
        {
            var apple = MakeFood(3001);
            var otherInventory = new GameObject("Other").AddComponent<InventoryManager>();
            otherInventory.AddItem(apple, 1);
            var foreignStack = otherInventory.GetAllItems()[0];

            Assert.IsFalse(FoodUse.TryEat(_status, null));
            Assert.IsFalse(
                FoodUse.TryEat(_status, foreignStack),
                "この在庫に入っていないスタックは使えない"
            );
            Object.DestroyImmediate(otherInventory.gameObject);
        }

        [Test]
        public void TryEat_LastOne_RemovesStackFromInventory()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(1f);

            Assert.IsTrue(FoodUse.TryEat(_status, StackOf(apple)));

            Assert.IsNull(StackOf(apple), "使い切ったスタックは在庫から消える");
        }

        [Test]
        public void TryEat_WhileDead_DoesNotConsume()
        {
            // 死亡中は回復しないので、食材だけ減らさない。
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 2);
            TestReflection.SetField(_status, "<IsDead>k__BackingField", true);

            Assert.IsFalse(FoodUse.TryEat(_status, StackOf(apple)));

            Assert.AreEqual(2, StackOf(apple).Count, "在庫は減らない");
        }
    }
}
