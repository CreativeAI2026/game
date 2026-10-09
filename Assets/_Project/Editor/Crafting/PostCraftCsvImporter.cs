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
    /// <summary>
    /// 調合結果(合成後アイテム)を CSV から作り、レシピ・アイテムのカタログへ同期する。
    /// 装備品と食材で CSV・出力先・完成品の型・素材の条件だけが違う。
    /// </summary>
    public static class PostCraftCsvImporter
    {
        private const string RecipeCatalogPath =
            "Assets/_Project/Resources/Crafting/CraftRecipeCatalog.asset";
        private const string ItemCatalogPath = "Assets/_Project/Resources/ItemCatalog.asset";
        private const string InventoryDataDirectory = "Assets/_Project/Features/Inventory/Data";

        private static readonly Kind Equipment = new(
            name: "Equipment",
            csvPath: "Assets/_Project/Editor/Crafting/PostCraftEquipment.csv",
            itemOutputDirectory: "Assets/_Project/Features/Inventory/Data/Equipment/PostCraft",
            category: ItemCategory.Equipment,
            loadOrCreate: LoadOrCreate<EquipmentData>,
            isValidMaterial: item => item is EquipmentData,
            materialDescription: "EquipmentData",
            applyResult: item =>
            {
                // 調合結果の能力値は素材からロールするので、完成品の固定値は持たない。
                var equipment = (EquipmentData)item;
                equipment.attack = 0;
                equipment.defense = 0;
                equipment.criticalDamage = 0;
                equipment.criticalRate = 0;
                equipment.maxHP = 0;
            }
        );

        private static readonly Kind Food = new(
            name: "Food",
            csvPath: "Assets/_Project/Editor/Crafting/PostCraftFood.csv",
            itemOutputDirectory: "Assets/_Project/Features/Inventory/Data/Food/PostCraft",
            category: ItemCategory.Food,
            loadOrCreate: LoadOrCreate<FoodData>,
            isValidMaterial: item => item is FoodData food && !food.IsCraftedResult,
            materialDescription: "合成前FoodData",
            applyResult: item =>
            {
                var serialized = new SerializedObject(item);
                serialized.FindProperty("_craftedResult").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        );

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft Equipment CSVを検証")]
        public static void ValidateEquipmentMenu() => ValidateMenu(Equipment);

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft EquipmentをCSVから同期")]
        public static void ImportEquipmentMenu() => ImportMenu(Equipment);

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft Food CSVを検証")]
        public static void ValidateFoodMenu() => ValidateMenu(Food);

        [MenuItem("Tools/CreativeAI/Crafting/PostCraft FoodをCSVから同期")]
        public static void ImportFoodMenu() => ImportMenu(Food);

        private static void ValidateMenu(Kind kind)
        {
            if (!TryLoadRows(kind, out List<Row> rows))
                return;

            List<string> errors = Validate(kind, rows);
            if (errors.Count == 0)
                Debug.Log($"{kind.LogTag} 検証成功: {rows.Count}件");
            else
                Debug.LogError(BuildErrorMessage(kind, errors));
        }

        private static void ImportMenu(Kind kind)
        {
            if (!TryLoadRows(kind, out List<Row> rows))
                return;

            List<string> errors = Validate(kind, rows);
            if (errors.Count > 0)
            {
                Debug.LogError(BuildErrorMessage(kind, errors));
                return;
            }

            EnsureDirectory(kind.ItemOutputDirectory);

            int spriteChanges = 0;
            foreach (Row row in rows)
            {
                if (ConfigureSprite(row.ImagePath))
                    spriteChanges++;
            }

            int createdItems = 0;
            int updatedItems = 0;
            var importedRecipes = new List<CraftRecipe>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Row row in rows)
                {
                    ItemData result = kind.LoadOrCreate(GetItemPath(kind, row), out bool created);
                    ApplyItem(kind, row, result);
                    if (created)
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

            SyncRecipeCatalog(importedRecipes);
            SyncItemCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"{kind.LogTag} 同期完了: Sprite変更={spriteChanges}, "
                    + $"ItemData 作成={createdItems}/更新={updatedItems}, "
                    + $"Recipe 同期={importedRecipes.Count}"
            );
        }

        private static bool TryLoadRows(Kind kind, out List<Row> rows)
        {
            rows = new List<Row>();
            string absolutePath = Path.GetFullPath(kind.CsvPath);
            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"{kind.LogTag} CSVがありません: {kind.CsvPath}");
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
                    Debug.LogError($"{kind.LogTag} {i + 1}行目: {exception.Message}");
                    return false;
                }
                if (columns.Count != 8)
                {
                    Debug.LogError(
                        $"{kind.LogTag} {i + 1}行目: 列数は8列必要です。現在={columns.Count}"
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
                    Debug.LogError($"{kind.LogTag} {i + 1}行目: idが整数ではありません。");
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

        private static List<string> Validate(Kind kind, IReadOnlyList<Row> rows)
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

                ValidateMaterial(kind, row, row.Material1Key, itemsByKey, errors);
                ValidateMaterial(kind, row, row.Material2Key, itemsByKey, errors);

                foreach (ItemData other in allItems)
                {
                    string otherPath = AssetDatabase.GetAssetPath(other);
                    bool isTarget = otherPath == GetItemPath(kind, row);
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
            Kind kind,
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

            if (!kind.IsValidMaterial(matches[0]))
                errors.Add(
                    $"{row.LineNumber}行目: 素材key={key}は{kind.MaterialDescription}ではありません。"
                );
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

        private static void ApplyItem(Kind kind, Row row, ItemData item)
        {
            Undo.RecordObject(item, "PostCraft ItemDataを同期");
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(row.ImagePath);
            item.id = row.Id;
            item.key = row.Key;
            item.itemName = row.ItemName;
            item.category = kind.Category;
            item.description = row.Description;
            kind.ApplyResult(item);
            EditorUtility.SetDirty(item);
        }

        private static CraftRecipe CreateRecipe(Row row, ItemData result) =>
            new()
            {
                resultItem = result,
                material1 = FindItem(row.Material1Key),
                material2 = FindItem(row.Material2Key),
            };

        private static ItemData FindItem(string key) =>
            LoadAllItems().Single(item => item != null && item.key == key);

        private static List<ItemData> LoadAllItems() =>
            AssetDatabase
                .FindAssets("t:ItemData", new[] { InventoryDataDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null)
                .ToList();

        private static string GetItemPath(Kind kind, Row row) =>
            $"{kind.ItemOutputDirectory}/{row.AssetName}.asset";

        private static ItemData LoadOrCreate<T>(string path, out bool created)
            where T : ItemData
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SyncRecipeCatalog(IReadOnlyCollection<CraftRecipe> importedRecipes)
        {
            CraftRecipeCatalog catalog = AssetDatabase.LoadAssetAtPath<CraftRecipeCatalog>(
                RecipeCatalogPath
            );
            if (catalog == null)
                throw new InvalidOperationException(
                    $"CraftRecipeCatalogがありません: {RecipeCatalogPath}"
                );

            CraftRecipeCatalogWriter.SetRecipes(catalog, importedRecipes);
        }

        private static void SyncItemCatalog()
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException($"ItemCatalogがありません: {ItemCatalogPath}");

            catalog.SyncFromInventoryDataFolder();
            EditorUtility.SetDirty(catalog);
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

        private static string BuildErrorMessage(Kind kind, IEnumerable<string> errors) =>
            $"{kind.LogTag} 同期を中止しました:\n- " + string.Join("\n- ", errors);

        private delegate ItemData LoadOrCreateItem(string path, out bool created);

        /// <summary>装備品・食材ごとに違う部分。</summary>
        private sealed class Kind
        {
            public string LogTag { get; }
            public string CsvPath { get; }
            public string ItemOutputDirectory { get; }
            public ItemCategory Category { get; }
            public LoadOrCreateItem LoadOrCreate { get; }
            public Func<ItemData, bool> IsValidMaterial { get; }
            public string MaterialDescription { get; }
            public Action<ItemData> ApplyResult { get; }

            public Kind(
                string name,
                string csvPath,
                string itemOutputDirectory,
                ItemCategory category,
                LoadOrCreateItem loadOrCreate,
                Func<ItemData, bool> isValidMaterial,
                string materialDescription,
                Action<ItemData> applyResult
            )
            {
                LogTag = $"[PostCraft {name} CSV]";
                CsvPath = csvPath;
                ItemOutputDirectory = itemOutputDirectory;
                Category = category;
                LoadOrCreate = loadOrCreate;
                IsValidMaterial = isValidMaterial;
                MaterialDescription = materialDescription;
                ApplyResult = applyResult;
            }
        }

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
