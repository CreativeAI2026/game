using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// インベントリのキーボード操作による決定(選択スロットの確定)の検証。
    /// </summary>
    public class InventoryViewKeyboardSubmitTests
    {
        private GameObject _root;
        private InventoryView _inventory;
        private ItemSlot _slot;
        private ItemData _item;
        private ItemStack _stack;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Inventory");
            _inventory = _root.AddComponent<InventoryView>();
            var slotObject = new GameObject("ItemSlot");
            slotObject.transform.SetParent(_root.transform);
            _slot = slotObject.AddComponent<ItemSlot>();
            _item = ScriptableObject.CreateInstance<ItemData>();
            _stack = new ItemStack(_item);
            _slot.SetItem(_stack);

            var visibleSlots = TestReflection.GetField<List<ItemSlot>>(_inventory, "_visibleSlots");
            visibleSlots.Add(_slot);
            _inventory.SelectSlot(_slot);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_item);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SubmitSelectedSlot_RaisesEventWithKeyboardSelectedStack()
        {
            ItemStack submitted = null;
            _inventory.OnSlotSubmitted += stack => submitted = stack;

            _inventory.SubmitSelectedSlot();

            Assert.AreSame(_stack, submitted);
        }

        [Test]
        public void SubmitSelectedSlot_WithNoSelection_DoesNotRaiseEvent()
        {
            bool raised = false;
            _inventory.OnSlotSubmitted += _ => raised = true;
            _inventory.ClearSelection();

            _inventory.SubmitSelectedSlot();

            Assert.IsFalse(raised);
        }
    }
}
