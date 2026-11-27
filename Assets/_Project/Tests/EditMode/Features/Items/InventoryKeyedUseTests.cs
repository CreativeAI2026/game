using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キー指定で未装備のアイテムを数えて消費する処理(useItem の一覧・正解判定・消費)の検証。
    /// </summary>
    public class InventoryKeyedUseTests
    {
        private GameObject _go;
        private InventoryManager _service;
        private ItemData _gear;
        private readonly List<ItemData> _created = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(InventoryManager));
            _service = _go.AddComponent<InventoryManager>();
            _gear = ScriptableObject.CreateInstance<ItemData>();
            _gear.key = "gear";
            _gear.category = ItemCategory.Equipment;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_gear);
            foreach (var data in _created)
                Object.DestroyImmediate(data);
            _created.Clear();
        }

        private ItemData Data(string key)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.key = key;
            _created.Add(data);
            return data;
        }

        private ItemStack AddInstance() =>
            _service.AddInstance(_gear, new List<RolledStat> { new("atk", 1f) });

        // 空いている装備の枠に入れて装備する。
        private void Equip(ItemStack stack) =>
            _service.Equipment.Set(_service.Equipment.GetSlots().ToList().IndexOf(null), stack);

        [Test]
        public void CountItem_SumsUnequippedStacks()
        {
            AddInstance();
            AddInstance();
            Equip(AddInstance());

            Assert.AreEqual(2, _service.CountItem("gear"));
        }

        [Test]
        public void CountItem_OnlyEquipped_ReturnsZero()
        {
            Equip(AddInstance());

            Assert.AreEqual(0, _service.CountItem("gear"));
        }

        [Test]
        public void Consume_NotEnough_ChangesNothing()
        {
            AddInstance();

            Assert.IsFalse(_service.Consume("gear", 2));
            Assert.AreEqual(1, _service.CountItem("gear"));
        }

        [Test]
        public void Consume_SpansStacks()
        {
            AddInstance();
            AddInstance();

            Assert.IsTrue(_service.Consume("gear", 2));
            Assert.AreEqual(0, _service.CountItem("gear"));
        }

        [Test]
        public void ConsumeFromStack_Instance_RemovesItFromInventory()
        {
            // 個体は Count が常に 1 で減らせないため、消費したら丸ごと取り除く必要がある。
            var instance = AddInstance();

            Assert.IsTrue(_service.ConsumeFromStack(instance, 1));
            Assert.AreEqual(0, _service.GetAllItems().Count);
        }

        [Test]
        public void Consume_ExactCount_ReturnsTrue()
        {
            AddInstance();

            Assert.IsTrue(_service.Consume("gear", 1));
        }

        [Test]
        public void Consume_DoesNotTouchEquipped()
        {
            AddInstance();
            Equip(AddInstance());

            Assert.IsFalse(_service.Consume("gear", 2));
            Assert.AreEqual(2, _service.GetAllItems().Count);
        }

        [Test]
        public void Consume_EmptyKeyOrBadCount_ReturnsFalse()
        {
            AddInstance();

            Assert.IsFalse(_service.Consume("", 1));
            Assert.IsFalse(_service.Consume("gear", 0));
        }

        [Test]
        public void GetUsableItemCounts_SumsUnequippedByKey()
        {
            AddInstance();
            AddInstance();
            Equip(AddInstance());

            var counts = _service.GetUsableItemCounts();

            Assert.AreEqual(1, counts.Count);
            Assert.AreEqual(_gear, counts[0].Data);
            Assert.AreEqual(2, counts[0].Count, "装備中の個体は数えない");
        }

        [Test]
        public void GetUsableItemCounts_MatchesCountItem()
        {
            // 一覧に出る個数と正解判定の個数がずれると「並んでいるのに不正解」になる。
            AddInstance();
            Equip(AddInstance());
            _service.AddItem(Data("apple"), 3);

            foreach (var (data, count) in _service.GetUsableItemCounts())
                Assert.AreEqual(_service.CountItem(data.key), count, data.key);
        }

        [Test]
        public void GetUsableItemCounts_ExcludesEquippedAndKeyless()
        {
            Equip(AddInstance());
            _service.AddItem(Data(null));
            _service.AddItem(Data(""));

            Assert.IsEmpty(_service.GetUsableItemCounts());
        }

        [Test]
        public void GetUsableItemCounts_KeepsInventoryOrder()
        {
            _service.AddItem(Data("grapes"));
            _service.AddItem(Data("bread"));
            _service.AddItem(Data("strawberry"));

            CollectionAssert.AreEqual(
                new[] { "grapes", "bread", "strawberry" },
                _service.GetUsableItemCounts().Select(item => item.Data.key).ToList(),
                "並べ替えず、インベントリと同じ順(最初に出た順)"
            );
        }
    }
}
