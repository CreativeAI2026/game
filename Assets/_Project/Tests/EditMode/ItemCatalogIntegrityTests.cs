using System.Collections.Generic;
using System.Linq;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ItemCatalog の整合性 — ID 重複・参照切れ・登録漏れ・レシピの組み立てに問題が無いことの検証。
    /// </summary>
    public sealed class ItemCatalogIntegrityTests
    {
        private static ItemCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogSync.CatalogPath);
            Assert.That(catalog, Is.Not.Null, ItemCatalogSync.CatalogPath);
            return catalog;
        }

        [Test]
        public void ItemCatalog_HasUniqueIdsAndKeys_AndListsEverySubAsset()
        {
            ItemCatalog catalog = LoadCatalog();
            Assert.That(catalog.Items, Is.Not.Empty);
            for (int i = 0; i < catalog.Items.Count; i++)
                Assert.That(
                    catalog.Items[i],
                    Is.Not.Null,
                    $"ItemCatalog.items[{i}] が参照切れです。"
                );

            AssertNoDuplicates(catalog.Items, item => item.id, "ItemData.id");
            AssertNoDuplicates(catalog.Items, item => item.key, "ItemData.key");

            var subAssets = AssetDatabase
                .LoadAllAssetsAtPath(ItemCatalogSync.CatalogPath)
                .OfType<ItemData>()
                .ToList();
            CollectionAssert.AreEquivalent(
                subAssets,
                catalog.Items,
                "ItemCatalog.items とサブアセットが一致しません。"
            );
        }

        [Test]
        public void PostCraftItems_HaveValidMaterials_AndCanBeFoundInEitherOrder()
        {
            ItemCatalog catalog = LoadCatalog();
            var postCraft = catalog
                .Items.Where(item => item.material1 != null || item.material2 != null)
                .ToList();
            Assert.That(postCraft, Is.Not.Empty);

            foreach (ItemData item in postCraft)
            {
                Assert.That(item.material1, Is.Not.Null, $"{item.name}: material1");
                Assert.That(item.material2, Is.Not.Null, $"{item.name}: material2");
                Assert.That(item.material1, Is.Not.SameAs(item.material2), item.name);
                Assert.That(
                    catalog.Items,
                    Has.Member(item.material1),
                    $"{item.name}: material1 がカタログ外"
                );
                Assert.That(
                    catalog.Items,
                    Has.Member(item.material2),
                    $"{item.name}: material2 がカタログ外"
                );
                Assert.That(
                    item.material1.material1,
                    Is.Null,
                    $"{item.name}: 合成後アイテムを素材にしている"
                );
                Assert.That(
                    item.material2.material1,
                    Is.Null,
                    $"{item.name}: 合成後アイテムを素材にしている"
                );

                Assert.That(
                    catalog.FindRecipe(item.material2, item.material1)?.resultItem,
                    Is.SameAs(item),
                    $"{item.name}: 素材を逆順にしても引けること"
                );
            }
        }

        private static void AssertNoDuplicates<TValue>(
            IEnumerable<ItemData> items,
            System.Func<ItemData, TValue> selector,
            string label
        )
        {
            var duplicates = items
                .GroupBy(selector)
                .Where(group => group.Count() > 1)
                .Select(group =>
                    $"{group.Key}: {string.Join(", ", group.Select(item => item.name))}"
                )
                .ToArray();
            Assert.That(
                duplicates,
                Is.Empty,
                $"{label}が重複しています:\n{string.Join("\n", duplicates)}"
            );
        }
    }
}
