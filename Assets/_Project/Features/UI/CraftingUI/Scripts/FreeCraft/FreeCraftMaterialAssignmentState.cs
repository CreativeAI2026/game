using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class FreeCraftMaterialAssignmentState
    {
        public const int RequiredSlotCount = 2;

        private readonly ItemStack[] _stacks = new ItemStack[RequiredSlotCount];

        public int SlotCount => _stacks.Length;

        public bool IsValidIndex(int index) => index >= 0 && index < _stacks.Length;

        public ItemStack GetStack(int index)
        {
            ValidateIndex(index);
            return _stacks[index];
        }

        public bool HasStack(int index)
        {
            return GetStack(index) != null;
        }

        public void SetStack(int index, ItemStack stack)
        {
            ValidateIndex(index);
            _stacks[index] = stack ?? throw new ArgumentNullException(nameof(stack));
        }

        public ItemStack ClearStack(int index)
        {
            ValidateIndex(index);
            var clearedStack = _stacks[index];
            _stacks[index] = null;
            return clearedStack;
        }

        public void ClearAll()
        {
            Array.Clear(_stacks, 0, _stacks.Length);
        }

        public int FindStackIndex(ItemStack stack)
        {
            if (stack == null)
                return -1;

            for (int i = 0; i < _stacks.Length; i++)
            {
                if (ReferenceEquals(_stacks[i], stack))
                    return i;
            }

            return -1;
        }

        public int FindFirstEmptyIndex()
        {
            for (int i = 0; i < _stacks.Length; i++)
            {
                if (_stacks[i] == null)
                    return i;
            }

            return -1;
        }

        public IReadOnlyList<ItemStack> GetAssignedStacks()
        {
            var assignedStacks = new List<ItemStack>(_stacks.Length);
            foreach (var stack in _stacks)
            {
                if (stack != null)
                    assignedStacks.Add(stack);
            }

            return assignedStacks;
        }

        private void ValidateIndex(int index)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }
    }
}
