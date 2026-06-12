using System;

namespace CreativeAI.Crafting
{
    /// <summary>調合が成立しなかった理由。</summary>
    public enum CraftError
    {
        None,
        CategoryMismatch, // カテゴリ跨ぎ(装備品×食材など)は不可
        WeaponNotAllowed, // 武器は調合不可
        RecipeNotFound, // カタログに該当ペアが無い(本番では起きない想定)
    }

    /// <summary>
    /// 調合の入口。カテゴリ検証 → RecipeHash でカタログ参照 → ステータスをロール、
    /// を束ねる。素材消費やインベントリ反映は呼び出し側(上位層)の責務。
    /// </summary>
    public sealed class CraftingService
    {
        private readonly ICraftingCatalog _catalog;
        private readonly CraftingStatRoller _roller;

        public CraftingService(ICraftingCatalog catalog, CraftingStatRoller roller)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _roller = roller ?? throw new ArgumentNullException(nameof(roller));
        }

        public bool TryCraft(
            CraftMaterial a,
            CraftMaterial b,
            IRandomSource rng,
            out CraftResult result,
            out CraftError error
        )
        {
            result = null;
            error = CraftError.None;

            if (a == null)
                throw new ArgumentNullException(nameof(a));
            if (b == null)
                throw new ArgumentNullException(nameof(b));
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            // 武器は調合不可(GameSystems.md 3.3)。
            if (a.Category == CraftCategory.Weapon || b.Category == CraftCategory.Weapon)
            {
                error = CraftError.WeaponNotAllowed;
                return false;
            }

            // カテゴリ跨ぎ不可(装備品同士 / 食材同士のみ)。
            if (a.Category != b.Category)
            {
                error = CraftError.CategoryMismatch;
                return false;
            }

            var recipe = RecipeHash.Of(a.ItemId, b.ItemId);
            if (!_catalog.TryGetResult(recipe, out var resultItemId))
            {
                error = CraftError.RecipeNotFound;
                return false;
            }

            var stats = _roller.Roll(a.Stats, b.Stats, rng);
            result = new CraftResult(resultItemId, stats);
            return true;
        }
    }
}
