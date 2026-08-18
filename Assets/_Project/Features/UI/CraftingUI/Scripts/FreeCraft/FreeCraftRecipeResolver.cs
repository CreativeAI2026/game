using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;

namespace CreativeAI.UI.CraftingUI
{
    public enum FreeCraftRecipeFailure
    {
        None,
        MissingMaterials,
        RecipeNotFound,
    }

    public readonly struct FreeCraftRecipeResolution
    {
        private FreeCraftRecipeResolution(CraftRecipeData recipe, FreeCraftRecipeFailure failure)
        {
            Recipe = recipe;
            Failure = failure;
        }

        public bool Succeeded => Recipe != null && Failure == FreeCraftRecipeFailure.None;
        public CraftRecipeData Recipe { get; }
        public FreeCraftRecipeFailure Failure { get; }

        public static FreeCraftRecipeResolution Success(CraftRecipeData recipe)
        {
            return new FreeCraftRecipeResolution(
                recipe ?? throw new ArgumentNullException(nameof(recipe)),
                FreeCraftRecipeFailure.None
            );
        }

        public static FreeCraftRecipeResolution Failed(FreeCraftRecipeFailure failure)
        {
            if (failure == FreeCraftRecipeFailure.None)
                throw new ArgumentOutOfRangeException(nameof(failure));

            return new FreeCraftRecipeResolution(null, failure);
        }
    }

    public readonly struct FreeCraftRequest
    {
        public FreeCraftRequest(
            CraftRecipeData recipe,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        )
        {
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            FirstMaterial = firstMaterial ?? throw new ArgumentNullException(nameof(firstMaterial));
            SecondMaterial =
                secondMaterial ?? throw new ArgumentNullException(nameof(secondMaterial));
        }

        public CraftRecipeData Recipe { get; }
        public ItemStack FirstMaterial { get; }
        public ItemStack SecondMaterial { get; }
    }

    public sealed class FreeCraftRecipeResolver
    {
        private readonly CraftRecipeDB _recipeDatabase;

        public FreeCraftRecipeResolver(CraftRecipeDB recipeDatabase)
        {
            _recipeDatabase =
                recipeDatabase ?? throw new ArgumentNullException(nameof(recipeDatabase));
        }

        public FreeCraftRecipeResolution Resolve(IReadOnlyList<ItemStack> assignedStacks)
        {
            if (assignedStacks == null)
                throw new ArgumentNullException(nameof(assignedStacks));

            if (
                assignedStacks.Count < FreeCraftMaterialAssignmentState.RequiredSlotCount
                || assignedStacks[0]?.Data == null
                || assignedStacks[1]?.Data == null
            )
            {
                return FreeCraftRecipeResolution.Failed(FreeCraftRecipeFailure.MissingMaterials);
            }

            var recipe = _recipeDatabase.FindRecipe(assignedStacks[0].Data, assignedStacks[1].Data);
            return recipe != null
                ? FreeCraftRecipeResolution.Success(recipe)
                : FreeCraftRecipeResolution.Failed(FreeCraftRecipeFailure.RecipeNotFound);
        }
    }
}
