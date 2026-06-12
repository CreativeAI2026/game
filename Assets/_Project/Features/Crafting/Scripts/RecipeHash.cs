using System;

namespace CreativeAI.Crafting
{
    /// <summary>
    /// 素材ペアのキー(CraftingArchitecture.md「RecipeHash」)。
    /// 順不同: Of(A,B) == Of(B,A)。カタログ recipes の参照キーに使う。
    /// </summary>
    public readonly struct RecipeHash : IEquatable<RecipeHash>
    {
        public readonly int Low;
        public readonly int High;

        private RecipeHash(int low, int high)
        {
            Low = low;
            High = high;
        }

        public static RecipeHash Of(int itemIdA, int itemIdB)
        {
            return itemIdA <= itemIdB
                ? new RecipeHash(itemIdA, itemIdB)
                : new RecipeHash(itemIdB, itemIdA);
        }

        public bool Equals(RecipeHash other) => Low == other.Low && High == other.High;

        public override bool Equals(object obj) => obj is RecipeHash other && Equals(other);

        public override int GetHashCode() => unchecked((Low * 397) ^ High);

        public override string ToString() => $"recipe({Low}+{High})";
    }
}
