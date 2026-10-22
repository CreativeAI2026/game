using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 即時食材キー(1/2/3)での使用の検証。セットした枠の食材で回復し、所持数が1減る。
    /// 空き枠・パネル表示中・死亡中は何も起きない。
    /// </summary>
    public class PlayerQuickFoodUserTests
    {
        private GameObject _inventoryGo;
        private InventoryManager _inventory;
        private GameObject _playerGo;
        private PlayerInputHandler _input;
        private PlayerStatus _status;
        private PlayerQuickFoodUser _user;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _inventoryGo = new GameObject("InventoryManager");
            _inventory = _inventoryGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inventory);

            var playerData = ScriptableObject.CreateInstance<PlayerParameterData>();
            playerData.baseMaxLife = 1000f;
            _assets.Add(playerData);

            _playerGo = new GameObject("Player");
            _status = _playerGo.AddComponent<PlayerStatus>();
            TestReflection.SetField(_status, "_playerData", playerData);
            _user = _playerGo.AddComponent<PlayerQuickFoodUser>();
            _input = _playerGo.GetComponent<PlayerInputHandler>();
            TestReflection.SetField(_inventory, "_playerStatus", _status);

            // EditMode では Awake / OnEnable が走らないので明示的に呼ぶ。
            TestReflection.Invoke(_input, "OnEnable");
            TestReflection.Invoke(_user, "Awake");
            TestReflection.Invoke(_user, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_user, "OnDisable");
            TestReflection.Invoke(_input, "OnDisable");
            UiPanelService.ResetState();
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_inventoryGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private ItemStack AddFoodToSlot(int slot, int count)
        {
            var food = ScriptableObject.CreateInstance<FoodData>();
            food.id = 3001 + slot;
            _assets.Add(food);
            _inventory.AddItem(food, count);
            var stack = _inventory.GetAllItems().Find(s => s.Data == food);
            Assert.IsTrue(_inventory.SetQuickFood(slot, stack));
            return stack;
        }

        [Test]
        public void PressingKey_UsesFoodInThatSlot_HealsAndConsumesOne()
        {
            AddFoodToSlot(0, 1);
            var second = AddFoodToSlot(1, 3);
            _status.RestoreHp(100f);

            _input.QuickFoodInput(1, true); // 2キー

            Assert.AreEqual(300f, _status.CurrentHp, 1e-2f, "最大HP1000 の 20% = 200 回復");
            Assert.AreEqual(2, second.Count, "2枠目の所持数が1減る");
            Assert.AreSame(second, _inventory.GetQuickFoodSlots()[1], "残っていれば枠はそのまま");
        }

        [Test]
        public void UsingLastOne_ClearsTheSlot()
        {
            AddFoodToSlot(0, 1);
            _status.RestoreHp(100f);

            _input.QuickFoodInput(0, true);

            Assert.IsNull(_inventory.GetQuickFoodSlots()[0], "使い切った枠は空になる");
        }

        [Test]
        public void EmptySlot_DoesNothing()
        {
            _status.RestoreHp(100f);

            _input.QuickFoodInput(2, true);

            Assert.AreEqual(100f, _status.CurrentHp, 1e-2f);
        }

        [Test]
        public void KeyRelease_DoesNotUse()
        {
            var stack = AddFoodToSlot(0, 2);

            _input.QuickFoodInput(0, false);

            Assert.AreEqual(2, stack.Count, "押した瞬間だけ使う");
        }

        [Test]
        public void WhileAPanelIsOpen_DoesNotUse()
        {
            var stack = AddFoodToSlot(0, 2);
            UiPanelService.SetOpen(true);

            _input.QuickFoodInput(0, true);

            Assert.AreEqual(2, stack.Count, "インベ等の表示中は操作不能");
        }
    }
}
