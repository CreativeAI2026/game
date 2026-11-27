using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 装備の枠(入れる・付け替える・空ける・入れ替える・所持品から消えたら空ける)の検証。
    /// </summary>
    public class EquipmentSlotsTests
    {
        private readonly List<Object> _created = new();
        private readonly TestResidents _residents = new();
        private InventoryManager _inv;
        private EquipmentSlots _equipment;

        [SetUp]
        public void SetUp()
        {
            _inv = _residents.Add<InventoryManager>();
            _equipment = _inv.Equipment;
        }

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            foreach (var created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void Set_PutsStackInSlotAndEquipsIt()
        {
            var gear = AddItem(ItemCategory.Equipment);

            Assert.IsTrue(_equipment.Set(1, gear));

            Assert.AreSame(gear, _equipment.GetSlots()[1]);
            Assert.IsTrue(_inv.Equipment.Contains(gear));
        }

        [Test]
        public void Set_OccupiedSlot_UnequipsPrevious()
        {
            var before = AddItem(ItemCategory.Equipment);
            var after = AddItem(ItemCategory.Equipment);
            _equipment.Set(0, before);

            _equipment.Set(0, after);

            Assert.AreSame(after, _equipment.GetSlots()[0]);
            Assert.IsFalse(_inv.Equipment.Contains(before), "前に入っていた装備は外れる");
        }

        [Test]
        public void Set_StackInAnotherSlot_MovesIt()
        {
            var gear = AddItem(ItemCategory.Equipment);
            _equipment.Set(0, gear);

            _equipment.Set(2, gear);

            Assert.IsNull(_equipment.GetSlots()[0]);
            Assert.AreSame(gear, _equipment.GetSlots()[2]);
            Assert.IsTrue(_inv.Equipment.Contains(gear));
        }

        [Test]
        public void Set_RejectsOutOfRangeAndNonEquipment()
        {
            var gear = AddItem(ItemCategory.Equipment);
            var food = AddItem(ItemCategory.Food);

            Assert.IsFalse(_equipment.Set(EquipmentSlots.SlotCount, gear));
            Assert.IsFalse(_equipment.Set(-1, gear));
            Assert.IsFalse(_equipment.Set(0, food));
            Assert.IsFalse(_inv.Equipment.Contains(gear) || _inv.Equipment.Contains(food));
        }

        [Test]
        public void Clear_UnequipsSlot()
        {
            var gear = AddItem(ItemCategory.Equipment);
            _equipment.Set(0, gear);

            _equipment.Clear(0);

            Assert.IsNull(_equipment.GetSlots()[0]);
            Assert.IsFalse(_inv.Equipment.Contains(gear));
        }

        [Test]
        public void Swap_KeepsBothEquipped()
        {
            var first = AddItem(ItemCategory.Equipment);
            var second = AddItem(ItemCategory.Equipment);
            _equipment.Set(0, first);
            _equipment.Set(1, second);

            Assert.IsTrue(_equipment.Swap(0, 1));

            Assert.AreSame(second, _equipment.GetSlots()[0]);
            Assert.AreSame(first, _equipment.GetSlots()[1]);
            Assert.IsTrue(_inv.Equipment.Contains(first) && _inv.Equipment.Contains(second));
        }

        [Test]
        public void RemovedFromInventory_ClearsSlot()
        {
            var gear = AddItem(ItemCategory.Equipment);
            _equipment.Set(1, gear);

            _inv.Clear();

            Assert.IsNull(_equipment.GetSlots()[1]);
            Assert.IsFalse(_inv.Equipment.Contains(gear));
        }

        private ItemStack AddItem(ItemCategory category)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.category = category;
            data.id = 9100 + _created.Count;
            _created.Add(data);
            return _inv.AddItem(data, 1);
        }
    }
}
