using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キャラクター画面の装備枠(装備の枠を映す・付け替え・入れ替え・入れる先の選び方)の検証。
    /// </summary>
    public class LoadoutAssignmentTests
    {
        private readonly List<Object> _created = new();
        private GameObject _root;
        private InventoryManager _inventory;
        private readonly TestResidents _residents = new();
        private EquipmentSlots _equipment;
        private QuickFoodSlots _quickFood;
        private LoadoutAssignment _slots;

        [SetUp]
        public void SetUp()
        {
            _inventory = _residents.Add<InventoryManager>();
            _equipment = _inventory.Equipment;
            _quickFood = _inventory.QuickFood;

            _root = new GameObject("Loadout", typeof(RectTransform));
            _created.Add(_root);
            _slots = new LoadoutAssignment(CreateSlots(), CreateSlots());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
            _residents.Clear();
        }

        [Test]
        public void RefreshEquipment_ShowsEquipmentSlots()
        {
            var first = AddEquipment(1);
            var second = AddEquipment(2);
            _equipment.Set(0, first);
            _equipment.Set(2, second);

            _slots.RefreshEquipment();

            Assert.AreSame(first, _slots.EquipmentSlots[0].Stack);
            Assert.IsNull(_slots.EquipmentSlots[1].Stack);
            Assert.AreSame(second, _slots.EquipmentSlots[2].Stack);
        }

        [Test]
        public void RemoveEquipment_ClearsSelectedSlot()
        {
            var gear = AddEquipment(1);
            _slots.SelectedEquipment = 0;
            _slots.AssignEquipment(gear);

            Assert.IsTrue(_slots.RemoveEquipment());

            Assert.IsFalse(_inventory.Equipment.Contains(gear));
            Assert.IsNull(_slots.EquipmentSlots[0].Stack);
        }

        [Test]
        public void AssignEquipment_ReplacesItemInSelectedSlot()
        {
            var before = AddEquipment(1);
            var after = AddEquipment(2);
            _slots.SelectedEquipment = 0;
            _slots.AssignEquipment(before);

            bool assigned = _slots.AssignEquipment(after);

            Assert.IsTrue(assigned);
            Assert.IsFalse(_inventory.Equipment.Contains(before), "前に入っていた装備は外れる");
            Assert.IsTrue(_inventory.Equipment.Contains(after));
            Assert.AreSame(after, _slots.EquipmentSlots[0].Stack);
        }

        [Test]
        public void SwapEquipment_SwapsSlotsWithoutChangingEquippedState()
        {
            var first = AddEquipment(1);
            var second = AddEquipment(2);
            _equipment.Set(0, first);
            _equipment.Set(1, second);

            bool swapped = _slots.SwapEquipment(0, 1);

            Assert.IsTrue(swapped);
            Assert.AreSame(second, _slots.EquipmentSlots[0].Stack);
            Assert.AreSame(first, _slots.EquipmentSlots[1].Stack);
            Assert.AreSame(second, _equipment.GetSlots()[0], "並びは装備側に残る");
            Assert.IsTrue(
                _inventory.Equipment.Contains(first) && _inventory.Equipment.Contains(second)
            );
        }

        [Test]
        public void SelectTargetFor_PicksFirstEmptySlot()
        {
            var gear = AddEquipment(1);
            _slots.SelectedEquipment = 0;
            _slots.AssignEquipment(gear);

            _slots.SelectTargetFor(ItemCategory.Equipment);

            Assert.AreEqual(1, _slots.SelectedEquipment);
        }

        [Test]
        public void SelectTargetFor_KeepsSelectionWhenAllSlotsAreFull()
        {
            for (int i = 0; i < 3; i++)
            {
                _slots.SelectedEquipment = i;
                _slots.AssignEquipment(AddEquipment(i + 1));
            }
            _slots.SelectedEquipment = 2;

            _slots.SelectTargetFor(ItemCategory.Equipment);

            Assert.AreEqual(2, _slots.SelectedEquipment);
        }

        private LoadoutAssignmentSlotView[] CreateSlots()
        {
            var slots = new LoadoutAssignmentSlotView[3];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"Slot{i}", typeof(RectTransform));
                slot.transform.SetParent(_root.transform, false);
                slots[i] = slot.AddComponent<LoadoutAssignmentSlotView>();
            }
            return slots;
        }

        private ItemStack AddEquipment(int id)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.category = ItemCategory.Equipment;
            data.id = 9000 + id;
            _created.Add(data);
            _inventory.AddItem(data, 1);
            return _inventory.GetAllItems().Find(stack => stack.Data == data);
        }
    }
}
