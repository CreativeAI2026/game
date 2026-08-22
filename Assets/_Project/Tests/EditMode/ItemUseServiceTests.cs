using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 食材使用の検証(HP即時回復をその場で適用し、在庫を1つ消費)。
    /// 回復量は最大HPに対する固定割合(合成前20%/合成後50%。documents/Specification.md §2.1, §2.2)。
    /// </summary>
    public class ItemUseServiceTests
    {
        private InventoryService _inv;
        private ItemUseService _use;
        private GameObject _playerGo;
        private PlayerStatus _status;
        private PlayerParameterData _playerData;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _inv = new InventoryService(new InventoryStorage());

            _playerData = ScriptableObject.CreateInstance<PlayerParameterData>();
            _playerData.baseMaxLife = 1000f;
            _playerData.baseAttackPower = 2000f;
            _playerData.baseDefense = 500f;
            _assets.Add(_playerData);

            _playerGo = new GameObject("Player");
            _status = _playerGo.AddComponent<PlayerStatus>();
            TestReflection.SetField(_status, "_playerData", _playerData);

            _use = new ItemUseService(_inv, _status);
        }

        [TearDown]
        public void TearDown()
        {
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
        public void TryUse_PreCraftFood_HealsTwentyPercentOfMaxHp_AndConsumesOne()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 3);
            _status.RestoreHp(100f); // 最大1000 のうち 100

            Assert.IsTrue(_use.TryUse(StackOf(apple)));

            Assert.AreEqual(300f, _status.CurrentHp, 1e-2f, "最大HP1000 の 20% = 200 回復");
            Assert.AreEqual(2, StackOf(apple).Count, "在庫が1つ減る");
        }

        [Test]
        public void TryUse_PostCraftFood_HealsFiftyPercentOfMaxHp()
        {
            var soup = MakeFood(3101, crafted: true);
            _inv.AddItem(soup, 1);
            _status.RestoreHp(100f);

            Assert.IsTrue(_use.TryUse(StackOf(soup)));

            Assert.AreEqual(600f, _status.CurrentHp, 1e-2f, "最大HP1000 の 50% = 500 回復");
        }

        [Test]
        public void TryUse_DoesNotOverheal()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(950f);

            Assert.IsTrue(_use.TryUse(StackOf(apple)));

            Assert.AreEqual(1000f, _status.CurrentHp, 1e-2f, "最大HP を超えない");
        }

        [Test]
        public void TryUse_HealScalesWithBuffedMaxHp()
        {
            // 装備の最大HP% で最大HPが増えると、回復量(割合)も増える。
            _status.SetEquipment(new EquipmentBonus { maxHpPct = 100f }); // 最大HP 2000
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(0f);

            Assert.IsTrue(_use.TryUse(StackOf(apple)));

            Assert.AreEqual(400f, _status.CurrentHp, 1e-2f, "最大HP2000 の 20% = 400");
        }

        [Test]
        public void TryUse_NonFood_IsRejected()
        {
            var gear = ScriptableObject.CreateInstance<EquipmentData>();
            gear.id = 2001;
            _assets.Add(gear);
            _inv.AddItem(gear, 1);
            _status.RestoreHp(100f);

            Assert.IsFalse(_use.TryUse(StackOf(gear)), "食材以外は使用できない");
            Assert.AreEqual(100f, _status.CurrentHp, 1e-2f);
            Assert.AreEqual(1, StackOf(gear).Count, "在庫も減らさない");
        }

        [Test]
        public void TryUse_NullOrForeignStack_IsRejected()
        {
            var apple = MakeFood(3001);
            var otherInventory = new InventoryService(new InventoryStorage());
            otherInventory.AddItem(apple, 1);
            var foreignStack = otherInventory.GetAllItems()[0];

            Assert.IsFalse(_use.TryUse(null));
            Assert.IsFalse(_use.TryUse(foreignStack), "この在庫に入っていないスタックは使えない");
        }

        [Test]
        public void TryUse_LastOne_RemovesStackFromInventory()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _status.RestoreHp(0f);

            Assert.IsTrue(_use.TryUse(StackOf(apple)));

            Assert.IsNull(StackOf(apple), "使い切ったスタックは在庫から消える");
        }

        [Test]
        public void TryUse_WithoutPlayerStatus_DoesNotConsume()
        {
            // 回復先が居ないなら使用そのものを中止する。効果を出せないのに食材だけ消えるのは
            // spec §2.2「効果適用: HP即時回復をその場で適用」に反する(使用と効果は不可分)。
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 2);
            var useWithoutPlayer = new ItemUseService(_inv, null);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("PlayerStatus が見つからない")
            );

            Assert.IsFalse(useWithoutPlayer.TryUse(StackOf(apple)));

            Assert.AreEqual(2, StackOf(apple).Count, "在庫は減らない");
        }
    }
}
