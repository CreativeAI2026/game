using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    public static class PostCraftEquipmentCsvImporter
    {
        private const string CsvPath = "Assets/_Project/Editor/Crafting/PostCraftEquipment.csv";
        private const string ItemOutputDirectory =
            "Assets/_Project/Features/Inventory/Data/Equipment/PostCraft";
        private const string RecipeDatabasePath =
            "Assets/_Project/Resources/Crafting/CraftRecipeDB.asset";
        private const string ItemDatabasePath = "Assets/_Project/Resources/ItemDB.asset";
        private const string InventoryDataDirectory = "Assets/_Project/Features/Inventory/Data";

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft CSVを検証")]
        public static void ValidateMenu()
        {
            if (!TryLoadRows(out List<Row> rows))
                return;

            List<string> errors = Validate(rows);
            if (errors.Count == 0)
                Debug.Log($"[PostCraft CSV] 検証成功: {rows.Count}件");
            else
                Debug.LogError(BuildErrorMessage(errors));
        }

        [MenuItem("Tools/CreativeAI/Crafting/PostCraftをCSVから同期")]
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

            int spriteChanges = 0;
            int createdItems = 0;
            int updatedItems = 0;
            var importedRecipes = new List<CraftRecipe>();

            foreach (Row row in rows)
            {
                if (ConfigureSprite(row.ImagePath))
                    spriteChanges++;
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Row row in rows)
                {
                    EquipmentData result = LoadOrCreate<EquipmentData>(
                        $"{ItemOutputDirectory}/{row.AssetName}.asset",
                        out bool itemCreated
                    );
                    ApplyItem(row, result);
                    if (itemCreated)
                        createdItems++;
                    else
                        updatedItems++;

                    importedRecipes.Add(CreateRecipe(row, result));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            SyncRecipeDatabase(importedRecipes);
            SyncItemDatabase();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[PostCraft CSV] 同期完了: Sprite変更={spriteChanges}, "
                    + $"ItemData 作成={createdItems}/更新={updatedItems}, "
                    + $"Recipe 同期={importedRecipes.Count}"
            );
        }

        private static bool TryLoadRows(out List<Row> rows)
        {
            rows = new List<Row>();
            string absolutePath = Path.GetFullPath(CsvPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[PostCraft CSV] CSVがありません: {CsvPath}");
                return false;
            }

            string[] lines = File.ReadAllLines(absolutePath);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                IReadOnlyList<string> columns;
                try
                {
                    columns = CsvRecordParser.Parse(line);
                }
                catch (FormatException exception)
                {
                    Debug.LogError($"[PostCraft CSV] {i + 1}行目: {exception.Message}");
                    return false;
                }
                if (columns.Count != 8)
                {
                    Debug.LogError(
                        $"[PostCraft CSV] {i + 1}行目: 列数は8列必要です。現在={columns.Count}"
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
                    Debug.LogError($"[PostCraft CSV] {i + 1}行目: idが整数ではありません。");
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
                        columns[7].Trim()
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

            var sourceItems = AssetDatabase
                .FindAssets("t:ItemData", new[] { InventoryDataDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null)
                .ToList();
            var sourceByKey = sourceItems
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

                ValidateMaterial(row, row.Material1Key, sourceByKey, errors);
                ValidateMaterial(row, row.Material2Key, sourceByKey, errors);

                foreach (ItemData other in sourceItems)
                {
                    string otherPath = AssetDatabase.GetAssetPath(other);
                    bool isThisGeneratedAsset =
                        otherPath == $"{ItemOutputDirectory}/{row.AssetName}.asset";
                    if (!isThisGeneratedAsset && other.id == row.Id)
                        errors.Add(
                            $"{row.LineNumber}行目: id={row.Id} は {otherPath} と重複しています。"
                        );
                    if (!isThisGeneratedAsset && other.key == row.Key)
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
            IReadOnlyDictionary<string, List<ItemData>> sourceByKey,
            ICollection<string> errors
        )
        {
            if (!sourceByKey.TryGetValue(key, out List<ItemData> matches) || matches.Count != 1)
            {
                errors.Add(
                    $"{row.LineNumber}行目: 素材key={key}に一致するItemDataが1件ではありません。"
                );
                return;
            }

            if (matches[0] is not EquipmentData)
                errors.Add($"{row.LineNumber}行目: 素材key={key}はEquipmentDataではありません。");
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

        private static void ApplyItem(Row row, EquipmentData item)
        {
            Undo.RecordObject(item, "PostCraft ItemDataを同期");
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(row.ImagePath);
            item.id = row.Id;
            item.key = row.Key;
            item.itemName = row.ItemName;
            item.category = ItemCategory.Equipment;
            item.description = row.Description;
            item.attack = 0;
            item.defense = 0;
            item.criticalDamage = 0;
            item.criticalRate = 0;
            item.maxHP = 0;
            EditorUtility.SetDirty(item);
        }

        private static CraftRecipe CreateRecipe(Row row, EquipmentData result) =>
            new()
            {
                resultItem = result,
                material1 = FindItem(row.Material1Key),
                material2 = FindItem(row.Material2Key),
            };

        private static ItemData FindItem(string key) =>
            AssetDatabase
                .FindAssets("t:ItemData", new[] { InventoryDataDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Single(item => item != null && item.key == key);

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

        private static void SyncRecipeDatabase(IReadOnlyCollection<CraftRecipe> importedRecipes)
        {
            CraftRecipeDB database = AssetDatabase.LoadAssetAtPath<CraftRecipeDB>(
                RecipeDatabasePath
            );
            if (database == null)
                throw new InvalidOperationException(
                    $"CraftRecipeDBがありません: {RecipeDatabasePath}"
                );

            Undo.RecordObject(database, "CraftRecipeDBを同期");
            foreach (CraftRecipe recipe in importedRecipes)
                database.SetRecipe(recipe);
            EditorUtility.SetDirty(database);
        }

        private static void SyncItemDatabase()
        {
            ItemDB database = AssetDatabase.LoadAssetAtPath<ItemDB>(ItemDatabasePath);
            if (database == null)
                throw new InvalidOperationException($"ItemDBがありません: {ItemDatabasePath}");

            database.SyncFromInventoryDataFolder();
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
            "[PostCraft CSV] 同期を中止しました:\n- " + string.Join("\n- ", errors);

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

            public Row(
                int lineNumber,
                string imagePath,
                string assetName,
                int id,
                string key,
                string itemName,
                string description,
                string material1Key,
                string material2Key
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
            }
        }
    }
}
