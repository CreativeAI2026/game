using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.Crafting
{
    public static class PostCraftFoodCsvImporter
    {
        private const string CsvPath = "Assets/_Project/Editor/Crafting/PostCraftFood.csv";
        private const string ItemOutputDirectory =
            "Assets/_Project/Features/Inventory/Data/Food/PostCraft";
        private const string RecipeOutputDirectory = "Assets/_Project/Features/Crafting/Data/Food";
        private const string RecipeDatabasePath =
            "Assets/_Project/Resources/Crafting/CraftRecipeDB.asset";
        private const string InventoryDataDirectory = "Assets/_Project/Features/Inventory/Data";
        private const string LegacyItemPath =
            "Assets/_Project/Features/Inventory/Data/Food/GrapeMisoSoup.asset";
        private const string LegacyRecipePath =
            "Assets/_Project/Features/Crafting/Data/Grapes_MisoSoup.asset";
        private const string LegacyAssetName = "GrapesMisoSoup";

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft Food CSVを検証")]
        public static void ValidateMenu()
        {
            if (!TryLoadRows(out List<Row> rows))
                return;

            List<string> errors = Validate(rows);
            if (errors.Count == 0)
                Debug.Log($"[PostCraft Food CSV] 検証成功: {rows.Count}件");
            else
                Debug.LogError(BuildErrorMessage(errors));
        }

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft FoodをCSVから同期")]
        public static void ImportMenu()
        {
            if (!TryLoadRows(out List<Row> rows))
                return;

            List<string> errors = Validate(rows);
            if (errors.Count > 0)
            {
                Debug.LogError(BuildErrorMessage(errors));
                return;
            }

            EnsureDirectory(ItemOutputDirectory);
            EnsureDirectory(RecipeOutputDirectory);

            int spriteChanges = 0;
            foreach (Row row in rows)
            {
                if (ConfigureSprite(row.ImagePath))
                    spriteChanges++;
            }

            int createdItems = 0;
            int updatedItems = 0;
            int createdRecipes = 0;
            int updatedRecipes = 0;
            var importedRecipes = new List<CraftRecipeData>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Row row in rows)
                {
                    FoodData result = LoadOrCreate<FoodData>(
                        GetItemPath(row),
                        out bool itemCreated
                    );
                    ApplyItem(row, result);
                    if (itemCreated)
                        createdItems++;
                    else
                        updatedItems++;

                    CraftRecipeData recipe = LoadOrCreate<CraftRecipeData>(
                        GetRecipePath(row),
                        out bool recipeCreated
                    );
                    ApplyRecipe(row, result, recipe);
                    importedRecipes.Add(recipe);
                    if (recipeCreated)
                        createdRecipes++;
                    else
                        updatedRecipes++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            SyncRecipeDatabase(importedRecipes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[PostCraft Food CSV] 同期完了: Sprite変更={spriteChanges}, "
                    + $"FoodData 作成={createdItems}/更新={updatedItems}, "
                    + $"Recipe 作成={createdRecipes}/更新={updatedRecipes}"
            );
        }

        private static bool TryLoadRows(out List<Row> rows)
        {
            rows = new List<Row>();
            string absolutePath = Path.GetFullPath(CsvPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[PostCraft Food CSV] CSVがありません: {CsvPath}");
                return false;
            }

            string[] lines = File.ReadAllLines(absolutePath);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] columns = line.Split(',');
                if (columns.Length != 9)
                {
                    Debug.LogError(
                        $"[PostCraft Food CSV] {i + 1}行目: 列数は9列必要です。現在={columns.Length}"
                    );
                    return false;
                }

                if (
                    !int.TryParse(
                        columns[2].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int id
                    )
                )
                {
                    Debug.LogError($"[PostCraft Food CSV] {i + 1}行目: idが整数ではありません。");
                    return false;
                }

                if (!bool.TryParse(columns[8].Trim(), out bool showInRecipeCraft))
                {
                    Debug.LogError(
                        $"[PostCraft Food CSV] {i + 1}行目: showInRecipeCraftはtrue/falseで指定してください。"
                    );
                    return false;
                }

                rows.Add(
                    new Row(
                        i + 1,
                        columns[0].Trim(),
                        columns[1].Trim(),
                        id,
                        columns[3].Trim(),
                        columns[4].Trim(),
                        columns[5].Trim(),
                        columns[6].Trim(),
                        columns[7].Trim(),
                        showInRecipeCraft
                    )
                );
            }

            return true;
        }

        private static List<string> Validate(IReadOnlyList<Row> rows)
        {
            var errors = new List<string>();
            if (rows.Count == 0)
            {
                errors.Add("CSVにデータ行がありません。");
                return errors;
            }

            ValidateDuplicates(
                rows,
                row => row.Id.ToString(CultureInfo.InvariantCulture),
                "id",
                errors
            );
            ValidateDuplicates(rows, row => row.Key, "key", errors);
            ValidateDuplicates(rows, row => row.AssetName, "assetName", errors);
            ValidateDuplicates(rows, row => row.ImagePath, "imagePath", errors);

            List<ItemData> allItems = LoadAllItems();
            var itemsByKey = allItems
                .Where(item => !string.IsNullOrWhiteSpace(item.key))
                .GroupBy(item => item.key)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (Row row in rows)
            {
                if (
                    string.IsNullOrWhiteSpace(row.AssetName)
                    || string.IsNullOrWhiteSpace(row.Key)
                    || string.IsNullOrWhiteSpace(row.ItemName)
                )
                    errors.Add($"{row.LineNumber}行目: assetName/key/itemNameは必須です。");
                if (row.Material1Key == row.Material2Key)
                    errors.Add($"{row.LineNumber}行目: 同じ素材は2つ指定できません。");
                if (!File.Exists(Path.GetFullPath(row.ImagePath)))
                    errors.Add($"{row.LineNumber}行目: 画像がありません: {row.ImagePath}");

                ValidateMaterial(row, row.Material1Key, itemsByKey, errors);
                ValidateMaterial(row, row.Material2Key, itemsByKey, errors);

                foreach (ItemData other in allItems)
                {
                    string otherPath = AssetDatabase.GetAssetPath(other);
                    bool isTarget = otherPath == GetItemPath(row);
                    if (!isTarget && other.id == row.Id)
                        errors.Add(
                            $"{row.LineNumber}行目: id={row.Id} は {otherPath} と重複しています。"
                        );
                    if (!isTarget && other.key == row.Key)
                        errors.Add(
                            $"{row.LineNumber}行目: key={row.Key} は {otherPath} と重複しています。"
                        );
                }
            }

            return errors.Distinct().ToList();
        }

        private static void ValidateDuplicates(
            IReadOnlyList<Row> rows,
            Func<Row, string> selector,
            string label,
            ICollection<string> errors
        )
        {
            foreach (
                IGrouping<string, Row> duplicate in rows.GroupBy(selector)
                    .Where(group => group.Count() > 1)
            )
                errors.Add($"CSV内で{label}={duplicate.Key}が重複しています。");
        }

        private static void ValidateMaterial(
            Row row,
            string key,
            IReadOnlyDictionary<string, List<ItemData>> itemsByKey,
            ICollection<string> errors
        )
        {
            if (!itemsByKey.TryGetValue(key, out List<ItemData> matches) || matches.Count != 1)
            {
                errors.Add(
                    $"{row.LineNumber}行目: 素材key={key}に一致するItemDataが1件ではありません。"
                );
                return;
            }

            if (matches[0] is not FoodData food || food.IsCraftedResult)
                errors.Add($"{row.LineNumber}行目: 素材key={key}は合成前FoodDataではありません。");
        }

        private static bool ConfigureSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"TextureImporterを取得できません: {path}");

            bool changed =
                importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single;
            if (!changed)
                return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return true;
        }

        private static void ApplyItem(Row row, FoodData item)
        {
            Undo.RecordObject(item, "PostCraft FoodDataを同期");
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(row.ImagePath);
            item.id = row.Id;
            item.key = row.Key;
            item.itemName = row.ItemName;
            item.category = ItemCategory.Food;
            item.description = row.Description;

            var serialized = new SerializedObject(item);
            serialized.FindProperty("_craftedResult").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ApplyRecipe(Row row, FoodData result, CraftRecipeData recipe)
        {
            Undo.RecordObject(recipe, "PostCraft Food Recipeを同期");
            recipe.resultItem = result;
            recipe.material1 = FindItem(row.Material1Key);
            recipe.material2 = FindItem(row.Material2Key);
            recipe.showInRecipeCraft = row.ShowInRecipeCraft;
            EditorUtility.SetDirty(recipe);
        }

        private static ItemData FindItem(string key) =>
            LoadAllItems().Single(item => item != null && item.key == key);

        private static List<ItemData> LoadAllItems() =>
            AssetDatabase
                .FindAssets("t:ItemData", new[] { InventoryDataDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null)
                .ToList();

        private static string GetItemPath(Row row) =>
            row.AssetName == LegacyAssetName
                ? LegacyItemPath
                : $"{ItemOutputDirectory}/{row.AssetName}.asset";

        private static string GetRecipePath(Row row) =>
            row.AssetName == LegacyAssetName
                ? LegacyRecipePath
                : $"{RecipeOutputDirectory}/{row.AssetName}.asset";

        private static T LoadOrCreate<T>(string path, out bool created)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SyncRecipeDatabase(IReadOnlyCollection<CraftRecipeData> importedRecipes)
        {
            CraftRecipeDB database = AssetDatabase.LoadAssetAtPath<CraftRecipeDB>(
                RecipeDatabasePath
            );
            if (database == null)
                throw new InvalidOperationException(
                    $"CraftRecipeDBがありません: {RecipeDatabasePath}"
                );

            var serialized = new SerializedObject(database);
            SerializedProperty recipes = serialized.FindProperty("_recipes");
            var merged = new List<CraftRecipeData>();
            for (int i = 0; i < recipes.arraySize; i++)
            {
                var recipe =
                    recipes.GetArrayElementAtIndex(i).objectReferenceValue as CraftRecipeData;
                if (recipe != null && !merged.Contains(recipe))
                    merged.Add(recipe);
            }
            foreach (CraftRecipeData recipe in importedRecipes)
            {
                if (!merged.Contains(recipe))
                    merged.Add(recipe);
            }

            recipes.arraySize = merged.Count;
            for (int i = 0; i < merged.Count; i++)
                recipes.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void EnsureDirectory(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Substring("Assets/".Length).Split('/'))
            {
                string next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static string BuildErrorMessage(IEnumerable<string> errors) =>
            "[PostCraft Food CSV] 同期を中止しました:\n- " + string.Join("\n- ", errors);

        private sealed class Row
        {
            public int LineNumber { get; }
            public string ImagePath { get; }
            public string AssetName { get; }
            public int Id { get; }
            public string Key { get; }
            public string ItemName { get; }
            public string Description { get; }
            public string Material1Key { get; }
            public string Material2Key { get; }
            public bool ShowInRecipeCraft { get; }

            public Row(
                int lineNumber,
                string imagePath,
                string assetName,
                int id,
                string key,
                string itemName,
                string description,
                string material1Key,
                string material2Key,
                bool showInRecipeCraft
            )
            {
                LineNumber = lineNumber;
                ImagePath = imagePath;
                AssetName = assetName;
                Id = id;
                Key = key;
                ItemName = itemName;
                Description = description;
                Material1Key = material1Key;
                Material2Key = material2Key;
                ShowInRecipeCraft = showInRecipeCraft;
            }
        }
    }
}
