using CreativeAI.Gameplay;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class RecipeCraftSelectionState
    {
        public bool HasCategory { get; private set; }
        public ItemCategory Category { get; private set; }
        public CraftRecipeData Recipe { get; private set; }
        public int Quantity { get; private set; } = 1;

        public void SelectCategory(ItemCategory category)
        {
            Category = category;
            HasCategory = true;
        }

        public void ClearCategory()
        {
            HasCategory = false;
        }

        public void SelectRecipe(CraftRecipeData recipe)
        {
            Recipe = recipe;
        }

        public void SetQuantity(int quantity)
        {
            Quantity = quantity < 1 ? 1 : quantity;
        }

        public void Reset()
        {
            ClearCategory();
            Recipe = null;
            Quantity = 1;
        }
    }
}
