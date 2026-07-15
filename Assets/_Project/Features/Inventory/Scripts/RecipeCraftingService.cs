using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Crafting service for CraftRecipeData-based recipes used by the current recipe UI.
    /// This service consumes materials only as inventory items; it does not trigger item use effects.
    /// </summary>
    public class RecipeCraftingService
    {
        private readonly InventoryService _inventoryService;

        public RecipeCraftingService(InventoryService inventoryService)
        {
            _inventoryService =
                inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        public bool CanCraft(CraftRecipeData recipe, int quantity = 1)
        {
            return TryResolveMaterialStacks(recipe, quantity, out _);
        }

        public bool CanCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return CanUseSelectedMaterialStacks(recipe, materialA, materialB);
        }

        public int GetMaximumCraftable(CraftRecipeData recipe)
        {
            if (!TryGetValidMaterials(recipe, out var materials))
                return 0;

            int max = int.MaxValue;
            foreach (var material in materials)
            {
                int available = _inventoryService
                    .GetAllItems()
                    .Where(stack => CanUseAsMaterial(stack, material, 1))
                    .Sum(stack => stack.Count);
                max = Math.Min(max, available);
            }

            return Math.Max(0, max);
        }

        public bool TryCraft(CraftRecipeData recipe, int quantity)
        {
            if (!TryResolveMaterialStacks(recipe, quantity, out var consumptions))
                return false;

            if (!CanConsumeAll(consumptions))
                return false;

            foreach (var consumption in consumptions)
            {
                bool consumed = _inventoryService.ConsumeFromStack(
                    consumption.Stack,
                    consumption.Count
                );
                if (!consumed)
                    return false;
            }

            _inventoryService.AddItem(recipe.resultItem, quantity);
            return true;
        }

        public bool TryCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            if (!CanUseSelectedMaterialStacks(recipe, materialA, materialB))
                return false;

            var consumptions = new List<StackConsumption> { new(materialA, 1), new(materialB, 1) };

            if (!CanConsumeAll(consumptions))
                return false;

            foreach (var consumption in consumptions)
            {
                bool consumed = _inventoryService.ConsumeFromStack(
                    consumption.Stack,
                    consumption.Count
                );
                if (!consumed)
                    return false;
            }

            _inventoryService.AddItem(recipe.resultItem, 1);
            return true;
        }

        private bool TryResolveMaterialStacks(
            CraftRecipeData recipe,
            int quantity,
            out List<StackConsumption> consumptions
        )
        {
            consumptions = null;

            if (recipe == null || recipe.resultItem == null || quantity <= 0)
                return false;

            if (!TryGetValidMaterials(recipe, out var materials))
                return false;

            consumptions = new List<StackConsumption>();
            var availableStacks = _inventoryService.GetAllItems();

            foreach (var material in materials)
            {
                int remaining = quantity;
                foreach (
                    var stack in availableStacks.Where(candidate =>
                        CanUseAsMaterial(candidate, material, 1)
                    )
                )
                {
                    int consumeCount = Math.Min(stack.Count, remaining);
                    consumptions.Add(new StackConsumption(stack, consumeCount));
                    remaining -= consumeCount;

                    if (remaining <= 0)
                        break;
                }

                if (remaining > 0)
                    return false;
            }

            return consumptions.Count > 0;
        }

        private static bool CanUseAsMaterial(ItemStack stack, ItemData requiredItem, int count)
        {
            if (stack == null || stack.Data != requiredItem || stack.Count <= 0 || stack.IsEquipped)
                return false;

            return stack.Count >= count;
        }

        private bool CanUseSelectedMaterialStacks(
            CraftRecipeData recipe,
            ItemStack materialA,
            ItemStack materialB
        )
        {
            if (!TryGetValidMaterials(recipe, out _))
                return false;

            if (
                !CanUseAsSelectedMaterial(materialA)
                || !CanUseAsSelectedMaterial(materialB)
                || materialA == materialB
                || materialA.Data == materialB.Data
            )
                return false;

            return recipe.MatchesMaterials(materialA.Data, materialB.Data);
        }

        private static bool CanUseAsSelectedMaterial(ItemStack stack)
        {
            return stack != null && stack.Data != null && stack.Count > 0 && !stack.IsEquipped;
        }

        private static bool TryGetValidMaterials(
            CraftRecipeData recipe,
            out List<ItemData> materials
        )
        {
            materials = null;

            if (recipe == null || recipe.resultItem == null)
                return false;

            materials = recipe.Materials.ToList();
            if (materials.Count != 2 || materials.Any(material => material == null))
                return false;

            return materials[0] != materials[1];
        }

        private bool CanConsumeAll(List<StackConsumption> consumptions)
        {
            return consumptions != null
                && consumptions.All(consumption =>
                    CanUseAsSelectedMaterial(consumption.Stack)
                    && _inventoryService.ContainsStack(consumption.Stack)
                    && consumption.Stack.Count >= consumption.Count
                );
        }

        private readonly struct StackConsumption
        {
            public StackConsumption(ItemStack stack, int count)
            {
                Stack = stack;
                Count = count;
            }

            public ItemStack Stack { get; }
            public int Count { get; }
        }
    }
}
