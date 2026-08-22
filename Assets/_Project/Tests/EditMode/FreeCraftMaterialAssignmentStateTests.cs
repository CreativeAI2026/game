using System;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// フリー調合の素材スロット割り当て状態の検証。
    /// </summary>
    public class FreeCraftMaterialAssignmentStateTests
    {
        [Test]
        public void InitialState_HasTwoEmptySlots()
        {
            var state = new FreeCraftMaterialAssignmentState();

            Assert.AreEqual(FreeCraftMaterialAssignmentState.RequiredSlotCount, state.SlotCount);
            Assert.AreEqual(0, state.GetAssignedStacks().Count);
            Assert.AreEqual(0, state.FindFirstEmptyIndex());
            Assert.IsFalse(state.HasStack(0));
            Assert.IsFalse(state.HasStack(1));
        }

        [Test]
        public void SetAndClear_PreserveStackReference()
        {
            var state = new FreeCraftMaterialAssignmentState();
            var stack = new ItemStack(null, 3);

            state.SetStack(1, stack);

            Assert.AreSame(stack, state.GetStack(1));
            Assert.AreEqual(1, state.FindStackIndex(stack));
            Assert.AreSame(stack, state.ClearStack(1));
            Assert.IsFalse(state.HasStack(1));
        }

        [Test]
        public void ClearAll_RemovesEveryStack()
        {
            var state = new FreeCraftMaterialAssignmentState();
            state.SetStack(0, new ItemStack(null));
            state.SetStack(1, new ItemStack(null));

            state.ClearAll();

            Assert.AreEqual(0, state.GetAssignedStacks().Count);
            Assert.AreEqual(0, state.FindFirstEmptyIndex());
        }

        [Test]
        public void FindStackIndex_UsesReferenceIdentity()
        {
            var state = new FreeCraftMaterialAssignmentState();
            var first = new ItemStack(null);
            var second = new ItemStack(null);
            state.SetStack(0, first);

            Assert.AreEqual(0, state.FindStackIndex(first));
            Assert.AreEqual(-1, state.FindStackIndex(second));
        }

        [Test]
        public void GetAssignedStacks_DoesNotContainNull()
        {
            var state = new FreeCraftMaterialAssignmentState();
            var stack = new ItemStack(null);
            state.SetStack(1, stack);

            Assert.IsTrue(state.GetAssignedStacks().All(assigned => assigned != null));
            Assert.AreSame(stack, state.GetAssignedStacks().Single());
        }

        [Test]
        public void GetAssignedStacks_ReturnsSnapshotThatCannotMutateState()
        {
            var state = new FreeCraftMaterialAssignmentState();
            var stack = new ItemStack(null);
            var otherStack = new ItemStack(null);
            state.SetStack(0, stack);

            var snapshot = state.GetAssignedStacks();
            ((System.Collections.Generic.List<ItemStack>)snapshot)[0] = otherStack;

            Assert.AreSame(stack, state.GetStack(0));
        }

        [Test]
        public void InvalidIndexAndNullStack_Throw()
        {
            var state = new FreeCraftMaterialAssignmentState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.GetStack(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.ClearStack(2));
            Assert.Throws<ArgumentNullException>(() => state.SetStack(0, null));
        }
    }
}
