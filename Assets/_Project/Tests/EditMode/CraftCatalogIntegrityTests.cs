using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 調合カタログ(ItemDB / CraftRecipeDB)の整合性 — ID 重複・参照切れ・登録漏れが無いことの検証。
    /// </summary>
    public sealed class CraftCatalogIntegrityTests
    {
        private const string InventoryDataPath = "Assets/_Project/Features/Inventory/Data";
        private const string ItemDatabasePath = "Assets/_Project/Resources/ItemDB.asset";
        private const string RecipeDatabasePath =
            "Assets/_Project/Resources/Crafting/CraftRecipeDB.asset";

        [Test]
        public void ItemCatalog_HasUniqueIdsAndKeys_AndContainsEveryInventoryItem()
        {
            List<ItemData> items = LoadAssets<ItemData>(InventoryDataPath);
            Assert.That(items, Is.Not.Empty);

            AssertNoDuplicates(items, item => item.id, "ItemData.id");
            AssertNoDuplicates(items, item => item.key, "ItemData.key");

            ItemDB database = AssetDatabase.LoadAssetAtPath<ItemDB>(ItemDatabasePath);
            Assert.That(database, Is.Not.Null, ItemDatabasePath);
            var serialized = new SerializedObject(database);
            SerializedProperty entries = serialized.FindProperty("items");
            var registered = new HashSet<ItemData>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var item = entries.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
                Assert.That(item, Is.Not.Null, $"ItemDB.items[{i}] が参照切れです。");
                Assert.That(registered.Add(item), Is.True, $"ItemDB内で重複: {item.name}");
            }

            CollectionAssert.AreEquivalent(
                items,
                registered,
                "ItemDBがInventory/Dataと一致しません。"
            );
        }

        [Test]
        public void RecipeCatalog_HasValidReferencesUniquePairs_AndContainsEveryRecipe()
        {
            List<CraftRecipeData> recipes = LoadAssets<CraftRecipeData>(
                "Assets/_Project/Features/Crafting/Data"
            );
            Assert.That(recipes, Is.Not.Empty);

            var pairs = new HashSet<string>();
            foreach (CraftRecipeData recipe in recipes)
            {
                Assert.That(recipe.resultItem, Is.Not.Null, $"{recipe.name}: resultItem");
                Assert.That(recipe.material1, Is.Not.Null, $"{recipe.name}: material1");
                Assert.That(recipe.material2, Is.Not.Null, $"{recipe.name}: material2");
                Assert.That(recipe.material1, Is.Not.SameAs(recipe.material2), recipe.name);

                string pair =
                    string.CompareOrdinal(recipe.material1.key, recipe.material2.key) < 0
                        ? $"{recipe.material1.key}|{recipe.material2.key}"
                        : $"{recipe.material2.key}|{recipe.material1.key}";
                Assert.That(pairs.Add(pair), Is.True, $"素材ペアが重複しています: {pair}");
            }

            CraftRecipeDB database = AssetDatabase.LoadAssetAtPath<CraftRecipeDB>(
                RecipeDatabasePath
            );
            Assert.That(database, Is.Not.Null, RecipeDatabasePath);
            CollectionAssert.AreEquivalent(
                recipes,
                database.Recipes,
                "CraftRecipeDBがDataと一致しません。"
            );
        }

        private static List<T> LoadAssets<T>(string folder)
            where T : UnityEngine.Object =>
            AssetDatabase
                .FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();

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
