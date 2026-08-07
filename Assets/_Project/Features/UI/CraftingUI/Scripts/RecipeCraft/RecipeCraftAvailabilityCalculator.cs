using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class RecipeCraftAvailabilityCalculator
    {
        public int GetMaximumCraftable(
            CraftRecipeData recipe,
            IReadOnlyList<ItemStack> inventorySnapshot,
            IReadOnlyList<ItemStack> quickFoodSnapshot = null
        )
        {
            if (!TryGetMaterials(recipe, out var materials))
                return 0;

            inventorySnapshot ??= Array.Empty<ItemStack>();
            int maximum = int.MaxValue;
            foreach (var material in materials)
            {
                int available = inventorySnapshot
                    .Where(stack => CanUseAsMaterial(stack, material, quickFoodSnapshot))
                    .Sum(stack => stack.Count);
                maximum = Math.Min(maximum, available);
            }

            return Math.Max(0, maximum);
        }

        public bool CanCraft(
            CraftRecipeData recipe,
            int quantity,
            IReadOnlyList<ItemStack> inventorySnapshot,
            IReadOnlyList<ItemStack> quickFoodSnapshot = null
        )
        {
            return quantity > 0
                && GetMaximumCraftable(recipe, inventorySnapshot, quickFoodSnapshot) >= quantity;
        }

        public bool HasEquippedMaterial(
            CraftRecipeData recipe,
            IReadOnlyList<ItemStack> inventorySnapshot
        )
        {
            if (
                GetMaximumCraftable(recipe, inventorySnapshot) > 0
                || !TryGetMaterials(recipe, out var materials)
            )
            {
                return false;
            }

            inventorySnapshot ??= Array.Empty<ItemStack>();
            return inventorySnapshot.Any(stack =>
                stack != null
                && stack.IsEquipped
                && stack.Count > 0
                && materials.Contains(stack.Data)
            );
        }

        public bool HasQuickFoodMaterial(
            CraftRecipeData recipe,
            int quantity,
            IReadOnlyList<ItemStack> inventorySnapshot,
            IReadOnlyList<ItemStack> quickFoodSnapshot
        )
        {
            if (quantity <= 0 || quickFoodSnapshot == null || quickFoodSnapshot.Count == 0)
                return false;

            return !CanCraft(recipe, quantity, inventorySnapshot, quickFoodSnapshot)
                && CanCraft(recipe, quantity, inventorySnapshot);
        }

        public IReadOnlyList<RecipeCraftMaterialRowData> BuildMaterialRows(
            CraftRecipeData recipe,
            int quantity,
            IReadOnlyList<ItemStack> inventorySnapshot
        )
        {
            if (recipe == null)
                return Array.Empty<RecipeCraftMaterialRowData>();

            inventorySnapshot ??= Array.Empty<ItemStack>();
            int requiredCount = Math.Max(1, quantity);
            return recipe
                .Materials.Where(material => material != null)
                .Select(material => new RecipeCraftMaterialRowData(
                    material,
                    requiredCount,
                    inventorySnapshot
                        .Where(stack => stack != null && stack.Data == material && stack.Count > 0)
                        .Sum(stack => stack.Count)
                ))
                .ToList();
        }

        private static bool TryGetMaterials(
            CraftRecipeData recipe,
            out IReadOnlyList<ItemData> materials
        )
        {
            materials = Array.Empty<ItemData>();
            if (recipe == null || recipe.resultItem == null)
                return false;

            var resolvedMaterials = recipe.Materials.Where(material => material != null).ToList();
            if (resolvedMaterials.Count != 2 || resolvedMaterials[0] == resolvedMaterials[1])
            {
                return false;
            }

            materials = resolvedMaterials;
            return true;
        }

        private static bool CanUseAsMaterial(
            ItemStack stack,
            ItemData material,
            IReadOnlyList<ItemStack> quickFoodSnapshot
        )
        {
            return stack != null
                && stack.Data == material
                && stack.Count > 0
                && !stack.IsEquipped
                && !ContainsReference(quickFoodSnapshot, stack);
        }

        private static bool ContainsReference(IReadOnlyList<ItemStack> stacks, ItemStack candidate)
        {
            if (stacks == null)
                return false;

            for (int i = 0; i < stacks.Count; i++)
            {
                if (ReferenceEquals(stacks[i], candidate))
                    return true;
            }

            return false;
        }
    }
}
