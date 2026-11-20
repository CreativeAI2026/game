using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キー指定で未装備のアイテムを数えて消費する処理(useItem の正解判定と消費)の検証。
    /// </summary>
    public class InventoryKeyedUseTests
    {
        private InventoryService _service;
        private ItemData _gear;

        [SetUp]
        public void SetUp()
        {
            _service = new InventoryService();
            _gear = ScriptableObject.CreateInstance<ItemData>();
            _gear.key = "gear";
            _gear.category = ItemCategory.Equipment;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gear);

        private ItemStack AddInstance() =>
            _service.AddInstance(_gear, new List<RolledStat> { new("atk", 1f) });

        [Test]
        public void CountByKey_SumsUnequippedStacks()
        {
            AddInstance();
            AddInstance();
            AddInstance().IsEquipped = true;

            Assert.AreEqual(2, _service.CountByKey("gear"));
        }

        [Test]
        public void CountByKey_OnlyEquipped_ReturnsZero()
        {
            AddInstance().IsEquipped = true;

            Assert.AreEqual(0, _service.CountByKey("gear"));
        }

        [Test]
        public void ConsumeByKey_NotEnough_ChangesNothing()
        {
            AddInstance();

            Assert.IsFalse(_service.ConsumeByKey("gear", 2));
            Assert.AreEqual(1, _service.CountByKey("gear"));
        }

        [Test]
        public void ConsumeByKey_SpansStacks()
        {
            AddInstance();
            AddInstance();

            Assert.IsTrue(_service.ConsumeByKey("gear", 2));
            Assert.AreEqual(0, _service.CountByKey("gear"));
        }

        [Test]
        public void ConsumeByKey_ExactCount_ReturnsTrue()
        {
            AddInstance();

            Assert.IsTrue(_service.ConsumeByKey("gear", 1));
        }

        [Test]
        public void ConsumeByKey_DoesNotTouchEquipped()
        {
            AddInstance();
            AddInstance().IsEquipped = true;

            Assert.IsFalse(_service.ConsumeByKey("gear", 2));
            Assert.AreEqual(2, _service.GetAllItems().Count);
        }

        [Test]
        public void ConsumeByKey_EmptyKeyOrBadCount_ReturnsFalse()
        {
            AddInstance();

            Assert.IsFalse(_service.ConsumeByKey("", 1));
            Assert.IsFalse(_service.ConsumeByKey("gear", 0));
        }
    }
}
