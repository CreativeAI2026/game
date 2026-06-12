using CreativeAI.Crafting;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class CraftingServiceTests
    {
        // --- 仮データ(モックカタログ) ---
        // 装備品: 100,101 → 合成結果 200 / 食材: 300,301 → 合成結果 400
        private static CraftingService NewService()
        {
            var catalog = new InMemoryCraftingCatalog().Add(100, 101, 200).Add(300, 301, 400);
            return new CraftingService(catalog, new CraftingStatRoller(CraftingParameters.Default));
        }

        private static CraftMaterial Equip(int id, params (StatType, float)[] stats) =>
            new CraftMaterial(id, CraftCategory.Equipment, StatVector.Of(stats));

        private static CraftMaterial Food(int id, params (StatType, float)[] stats) =>
            new CraftMaterial(id, CraftCategory.Food, StatVector.Of(stats));

        [Test]
        public void TryCraft_SameCategoryWithKnownRecipe_Succeeds()
        {
            var svc = NewService();
            var a = Equip(100, (StatType.AttackPct, 10));
            var b = Equip(101, (StatType.DefensePct, 8));

            bool ok = svc.TryCraft(a, b, new SystemRandomSource(1), out var result, out var error);

            Assert.IsTrue(ok);
            Assert.AreEqual(CraftError.None, error);
            Assert.AreEqual(200, result.ResultItemId);
            Assert.Greater(result.RolledStats.Count, 0);
        }

        [Test]
        public void TryCraft_OrderOfMaterials_DoesNotMatter()
        {
            var svc = NewService();
            svc.TryCraft(Equip(100), Equip(101), new SystemRandomSource(1), out var r1, out _);
            svc.TryCraft(Equip(101), Equip(100), new SystemRandomSource(1), out var r2, out _);
            Assert.AreEqual(r1.ResultItemId, r2.ResultItemId);
        }

        [Test]
        public void TryCraft_CategoryMismatch_Fails()
        {
            var svc = NewService();
            bool ok = svc.TryCraft(
                Equip(100),
                Food(300),
                new SystemRandomSource(1),
                out var result,
                out var error
            );

            Assert.IsFalse(ok);
            Assert.IsNull(result);
            Assert.AreEqual(CraftError.CategoryMismatch, error);
        }

        [Test]
        public void TryCraft_Weapon_IsRejected()
        {
            var svc = NewService();
            var weapon = new CraftMaterial(500, CraftCategory.Weapon, StatVector.Empty);
            bool ok = svc.TryCraft(
                weapon,
                Equip(100),
                new SystemRandomSource(1),
                out _,
                out var error
            );

            Assert.IsFalse(ok);
            Assert.AreEqual(CraftError.WeaponNotAllowed, error);
        }

        [Test]
        public void TryCraft_UnknownRecipe_Fails()
        {
            var svc = NewService();
            // カタログに無いペア(102,103)
            bool ok = svc.TryCraft(
                Equip(102),
                Equip(103),
                new SystemRandomSource(1),
                out _,
                out var error
            );

            Assert.IsFalse(ok);
            Assert.AreEqual(CraftError.RecipeNotFound, error);
        }

        [Test]
        public void TryCraft_FoodPair_Succeeds()
        {
            var svc = NewService();
            bool ok = svc.TryCraft(
                Food(300, (StatType.HealAmount, 50)),
                Food(301, (StatType.AttackPct, 5)),
                new SystemRandomSource(2),
                out var result,
                out _
            );

            Assert.IsTrue(ok);
            Assert.AreEqual(400, result.ResultItemId);
        }
    }
}
