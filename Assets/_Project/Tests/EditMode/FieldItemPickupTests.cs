using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Crafting;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// フィールドのアイテム拾得(documents/Specification.md §0, §2「拾得」, §2.1.1)。
    /// 「移動中(Field)のみ拾える / 戦闘中・会話イベント中は拾わない / 装備品は拾った瞬間にロールする」。
    /// </summary>
    public class FieldItemPickupTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _invGo;
        private InventoryManager _inventory;
        private GameObject _pickupGo;
        private FieldItemPickup _pickup;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);

            _invGo = new GameObject(nameof(InventoryManager));
            _inventory = _invGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inventory);

            _pickupGo = new GameObject("FieldItem", typeof(BoxCollider));
            _pickup = _pickupGo.AddComponent<FieldItemPickup>();
        }

        [TearDown]
        public void TearDown()
        {
            EventPlaybackService.SetPlaying(false);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            Object.DestroyImmediate(_pickupGo);
            Object.DestroyImmediate(_invGo);
            Object.DestroyImmediate(_gmmGo);
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T MakeItem<T>(int id)
            where T : ItemData
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.id = id;
            _created.Add(item);
            return item;
        }

        private void PlaceItem(ItemData item, int count = 1)
        {
            TestReflection.SetField(_pickup, "_item", item);
            TestReflection.SetField(_pickup, "_count", count);
        }

        [Test]
        public void Pickup_Food_AddsThatManyToInventory()
        {
            var apple = MakeItem<FoodData>(3001); // OnEnable が category=Food
            PlaceItem(apple, 3);

            Assert.IsTrue(_pickup.TryPickup());

            Assert.AreEqual(3, _inventory.GetItemCount(apple));
            Assert.IsTrue(_pickup.IsPicked);
        }

        [Test]
        public void Pickup_KeyItem_AddsToInventory()
        {
            var keyItem = MakeItem<ImportantData>(4001);
            PlaceItem(keyItem);

            Assert.IsTrue(_pickup.TryPickup());

            Assert.AreEqual(1, _inventory.GetItemCount(keyItem));
        }

        [Test]
        public void Pickup_Equipment_RollsOneInstancePerItem()
        {
            // 装備品は拾った瞬間にロールした「個体」として1個ずつ入る(§2.1.1)。
            var gear = MakeItem<EquipmentData>(2001); // OnEnable が category=Equipment
            gear.attack = 20; // 総パワー20の宣言(どの型に書いてあるかは結果に影響しない)
            PlaceItem(gear, 2);

            Assert.IsTrue(_pickup.TryPickup());

            var stacks = _inventory.GetAllItems().Where(s => s.Data == gear).ToList();
            Assert.AreEqual(2, stacks.Count, "個体差があるので数量でまとめず1個ずつ別スタック");
            foreach (var stack in stacks)
            {
                Assert.IsTrue(stack.IsInstance);
                Assert.AreEqual(1, stack.Count);
                Assert.IsNotNull(stack.RolledStats);
                Assert.GreaterOrEqual(stack.RolledStats.Count, 1);
                Assert.LessOrEqual(stack.RolledStats.Count, 2, "付与数は最大2つ");
                Assert.LessOrEqual(
                    stack.RolledStats.Sum(r => r.value),
                    20f + 1e-3f,
                    "付与量の合計はシードの総パワーを超えない"
                );
                foreach (var rolled in stack.RolledStats)
                    Assert.IsTrue(
                        System.Enum.TryParse<StatType>(rolled.stat, out var type)
                            && type != StatType.HealAmount,
                        $"装備品に付かない型がロールされた: {rolled.stat}"
                    );
            }
        }

        [Test]
        public void Pickup_Twice_AddsOnlyOnce()
        {
            var apple = MakeItem<FoodData>(3002);
            PlaceItem(apple);

            Assert.IsTrue(_pickup.TryPickup());
            Assert.IsFalse(_pickup.TryPickup(), "二重取得はしない");

            Assert.AreEqual(1, _inventory.GetItemCount(apple));
        }

        [Test]
        public void Pickup_InBattleMode_IsBlocked()
        {
            var apple = MakeItem<FoodData>(3003);
            PlaceItem(apple);
            _gmm.EnterBattle();

            Assert.IsFalse(_pickup.TryPickup(), "戦闘モード中は拾わない");

            Assert.AreEqual(0, _inventory.GetItemCount(apple));
            Assert.IsFalse(_pickup.IsPicked, "拾えなかったので次に拾える状態で残る");
        }

        [Test]
        public void Pickup_AfterBattle_WorksAgain()
        {
            var apple = MakeItem<FoodData>(3004);
            PlaceItem(apple);
            _gmm.EnterBattle();
            Assert.IsFalse(_pickup.TryPickup());

            _gmm.ExitBattle();

            Assert.IsTrue(_pickup.TryPickup());
            Assert.AreEqual(1, _inventory.GetItemCount(apple));
        }

        [Test]
        public void Pickup_DuringEventPlayback_IsBlocked()
        {
            var apple = MakeItem<FoodData>(3005);
            PlaceItem(apple);
            EventPlaybackService.SetPlaying(true);

            Assert.IsFalse(_pickup.TryPickup(), "会話イベント中は拾わない");

            Assert.AreEqual(0, _inventory.GetItemCount(apple));
        }

        [Test]
        public void Pickup_WithoutItem_IsRejected()
        {
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("Item が未設定")
            );

            Assert.IsFalse(_pickup.TryPickup());
        }

        [Test]
        public void Pickup_HidesSparkleEffect()
        {
            var apple = MakeItem<FoodData>(3006);
            PlaceItem(apple);
            var sparkle = new GameObject("Sparkle");
            sparkle.transform.SetParent(_pickupGo.transform);
            TestReflection.SetField(_pickup, "_sparkle", sparkle);

            Assert.IsTrue(_pickup.TryPickup());

            Assert.IsFalse(sparkle.activeSelf, "拾ったらキラキラエフェクトを消す(§0)");
        }

        [Test]
        public void OnTriggerEnter_NonPlayerCollider_DoesNotPick()
        {
            var apple = MakeItem<FoodData>(3007);
            PlaceItem(apple);
            var enemy = new GameObject("Enemy", typeof(BoxCollider));
            try
            {
                TestReflection.Invoke(_pickup, "OnTriggerEnter", enemy.GetComponent<Collider>());

                Assert.IsFalse(_pickup.IsPicked);
                Assert.AreEqual(0, _inventory.GetItemCount(apple));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void OnTriggerEnter_Player_Picks()
        {
            var apple = MakeItem<FoodData>(3008);
            PlaceItem(apple);
            var player = new GameObject("Player", typeof(BoxCollider)) { tag = "Player" };
            try
            {
                TestReflection.Invoke(_pickup, "OnTriggerEnter", player.GetComponent<Collider>());

                Assert.IsTrue(_pickup.IsPicked);
                Assert.AreEqual(1, _inventory.GetItemCount(apple));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
