using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>画像ファイル名からの key 解釈、items.json の読み込みと照合、ItemCatalog が素材からレシピを引けることの検証。</summary>
    public sealed class ItemCatalogSyncTests
    {
        [TearDown]
        public void TearDown() => ItemCatalog.InjectForTests(null);

        [Test]
        public void ParseFileName_PreCraft_ReturnsSnakeCaseKey()
        {
            Assert.That(
                ItemCatalogSync.ParseFileName(
                    "item_equipment_fluorescent-light",
                    "item_equipment_",
                    false
                ),
                Is.EqualTo(new[] { "fluorescent_light" })
            );
        }

        [Test]
        public void ParseFileName_PostCraft_ReturnsTwoMaterialKeys()
        {
            Assert.That(
                ItemCatalogSync.ParseFileName(
                    "item_food_miso-soup_orange-juice",
                    "item_food_",
                    true
                ),
                Is.EqualTo(new[] { "miso_soup", "orange_juice" })
            );
        }

        [TestCase("item_food_apple", "item_equipment_", false)] // 接頭辞違い
        [TestCase("item_food_apple_banana", "item_food_", false)] // 合成前に素材2つ
        [TestCase("item_food_apple", "item_food_", true)] // 合成後に素材1つ
        [TestCase("item_food__miso-soup_orange-juice", "item_food_", true)] // 区切りの重複
        [TestCase("item_food_Apple_banana", "item_food_", true)] // 大文字
        public void ParseFileName_InvalidFormat_ReturnsNull(
            string fileName,
            string prefix,
            bool postCraft
        )
        {
            Assert.That(ItemCatalogSync.ParseFileName(fileName, prefix, postCraft), Is.Null);
        }

        [Test]
        public void ToAssetName_ConvertsSnakeCaseToPascalCase()
        {
            Assert.That(
                ItemCatalogSync.ToAssetName("balloon_full_body_tights"),
                Is.EqualTo("BalloonFullBodyTights")
            );
        }

        [Test]
        public void ParseTexts_ReadsNameAndDescriptionByKey()
        {
            var errors = new System.Collections.Generic.List<string>();
            var texts = ItemCatalogSync.ParseTexts(
                "{ \"apple\": { \"name\": \"りんご\", \"description\": \"赤い\" } }",
                errors
            );

            Assert.That(errors, Is.Empty);
            Assert.That(texts["apple"].name, Is.EqualTo("りんご"));
            Assert.That(texts["apple"].description, Is.EqualTo("赤い"));
        }

        [Test]
        public void ParseTexts_InvalidJson_ReportsError()
        {
            var errors = new System.Collections.Generic.List<string>();

            Assert.That(ItemCatalogSync.ParseTexts("{ \"apple\": ", errors), Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void ValidateTexts_ReportsMissingExtraAndEmptyNames()
        {
            var texts = new System.Collections.Generic.Dictionary<string, ItemCatalogSync.ItemText>
            {
                {
                    "apple",
                    new ItemCatalogSync.ItemText { name = "りんご" }
                },
                {
                    "banana",
                    new ItemCatalogSync.ItemText { name = "" }
                },
                {
                    "typo",
                    new ItemCatalogSync.ItemText { name = "誤字" }
                },
            };

            var errors = ItemCatalogSync.ValidateTexts(
                new[] { "apple", "banana", "grapes" },
                texts
            );

            Assert.That(errors, Has.Count.EqualTo(3));
            Assert.That(errors, Has.Some.Contains("key=grapes がありません"));
            Assert.That(errors, Has.Some.Contains("key=typo に対応する画像がありません"));
            Assert.That(errors, Has.Some.Contains("key=banana の name が空です"));
        }

        [Test]
        public void FindRecipe_BuildsFromPostCraftMaterials_InEitherOrder()
        {
            var apple = ScriptableObject.CreateInstance<FoodData>();
            var banana = ScriptableObject.CreateInstance<FoodData>();
            var grapes = ScriptableObject.CreateInstance<FoodData>();
            var appleBanana = ScriptableObject.CreateInstance<FoodData>();
            appleBanana.material1 = apple;
            appleBanana.material2 = banana;
            ItemCatalog.InjectForTests(new ItemData[] { apple, banana, grapes, appleBanana });

            try
            {
                var catalog = ItemCatalog.Instance;
                Assert.That(catalog.FindRecipe(banana, apple)?.resultItem, Is.SameAs(appleBanana));
                Assert.That(catalog.FindRecipe(apple, banana)?.resultItem, Is.SameAs(appleBanana));
                Assert.That(catalog.FindRecipe(apple, grapes), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(apple);
                Object.DestroyImmediate(banana);
                Object.DestroyImmediate(grapes);
                Object.DestroyImmediate(appleBanana);
            }
        }
    }
}
