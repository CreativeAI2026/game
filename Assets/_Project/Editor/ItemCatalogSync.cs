using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CreativeAI.Gameplay;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// アイテム画像のフォルダと items.json から ItemCatalog(サブアセットとして全 ItemData を持つ1ファイル)を作り直す。
    /// 画像があってアイテムが無ければ作り、画像が無くなったアイテムは消す。
    /// key・アイコン・合成後の素材は画像のファイル名から、名前・説明は items.json から決める。
    /// 手動: Tools > CreativeAI > アイテムを同期 / バッチ:
    /// Unity -batchmode -quit -executeMethod CreativeAI.EditorTools.ItemCatalogSync.Run
    /// </summary>
    public static class ItemCatalogSync
    {
        public const string CatalogPath = "Assets/_Project/Resources/ItemCatalog.asset";
        public const string TextsPath = "Assets/_Project/Features/Inventory/items.json";
        private const string ImageRoot = "Assets/_Project/Art/UI/Items";
        private const string LogTag = "[ItemCatalogSync]";
        private const int IconMaxSize = 512;

        private static readonly Regex NamePart = new("^[a-z0-9]+(-[a-z0-9]+)*$");

        private static readonly Group[] Groups =
        {
            new(
                $"{ImageRoot}/Equipment/PreCraft",
                "item_equipment_",
                typeof(ItemData),
                ItemCategory.Equipment,
                false,
                2001
            ),
            new(
                $"{ImageRoot}/Equipment/PostCraft",
                "item_equipment_",
                typeof(ItemData),
                ItemCategory.Equipment,
                true,
                2101
            ),
            new(
                $"{ImageRoot}/Food/PreCraft",
                "item_food_",
                typeof(FoodData),
                ItemCategory.Food,
                false,
                3001
            ),
            new(
                $"{ImageRoot}/Food/PostCraft",
                "item_food_",
                typeof(FoodData),
                ItemCategory.Food,
                true,
                3101
            ),
            new(
                $"{ImageRoot}/Important",
                "item_importance_",
                typeof(ItemData),
                ItemCategory.Important,
                false,
                4001
            ),
        };

        [MenuItem("Tools/CreativeAI/アイテムを同期")]
        public static void SyncMenu() => Sync();

        /// <summary>バッチ実行の入口。</summary>
        public static void Run()
        {
            if (!Sync())
                EditorApplication.Exit(1);
        }

        /// <summary>同期する。エラーがあれば何も変えずに false。</summary>
        public static bool Sync()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"{LogTag} ItemCatalog がありません: {CatalogPath}");
                return false;
            }

            var errors = new List<string>();
            List<Entry> entries = CollectEntries(errors);
            Dictionary<string, ItemText> texts = null;
            if (File.Exists(TextsPath))
                texts = ParseTexts(File.ReadAllText(TextsPath), errors);
            else
                errors.Add($"{TextsPath} がありません。");
            if (texts != null)
                errors.AddRange(ValidateTexts(entries.Select(entry => entry.Key), texts));
            if (errors.Count > 0)
            {
                Debug.LogError($"{LogTag} 同期を中止しました:\n- " + string.Join("\n- ", errors));
                return false;
            }

            var existing = AssetDatabase
                .LoadAllAssetsAtPath(CatalogPath)
                .OfType<ItemData>()
                .ToList();
            var byKey = existing
                .GroupBy(item => (item.GetType(), item.category, item.key))
                .ToDictionary(group => group.Key, group => group.First());

            var kept = new HashSet<ItemData>();
            foreach (Entry entry in entries)
            {
                if (
                    byKey.TryGetValue(
                        (entry.Group.ItemType, entry.Group.Category, entry.Key),
                        out ItemData item
                    )
                )
                {
                    entry.Item = item;
                    kept.Add(item);
                }
            }

            int removed = 0;
            foreach (ItemData item in existing.Where(item => !kept.Contains(item)))
            {
                AssetDatabase.RemoveObjectFromAsset(item);
                UnityEngine.Object.DestroyImmediate(item, true);
                removed++;
            }

            int created = 0;
            foreach (Entry entry in entries.Where(entry => entry.Item == null))
            {
                var item = (ItemData)ScriptableObject.CreateInstance(entry.Group.ItemType);
                item.key = entry.Key;
                item.category = entry.Group.Category;
                item.id = NextId(entry.Group, entries);
                AssetDatabase.AddObjectToAsset(item, catalog);
                entry.Item = item;
                created++;
            }

            var itemByKey = entries.ToDictionary(entry => entry.Key, entry => entry.Item);
            foreach (Entry entry in entries)
                Apply(entry, itemByKey, texts[entry.Key]);

            catalog.SetItems(entries.Select(entry => entry.Item).OrderBy(item => item.id));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath);

            Debug.Log($"{LogTag} 同期完了: 作成={created} / 削除={removed} / 合計={entries.Count}");
            return true;
        }

        /// <summary>
        /// 画像のファイル名(拡張子なし)から key の元になる名前を取り出す。合成後は素材2つ、合成前は1つ。
        /// 形式が違えば null。例: item_food_apple_banana → ["apple", "banana"]
        /// </summary>
        public static string[] ParseFileName(string fileName, string prefix, bool postCraft)
        {
            if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
                return null;

            string[] parts = fileName.Substring(prefix.Length).Split('_');
            if (parts.Length != (postCraft ? 2 : 1) || !parts.All(NamePart.IsMatch))
                return null;
            return parts.Select(ToKey).ToArray();
        }

        /// <summary>items.json(key → 名前・説明)を読む。書式エラーは errors に積んで null。</summary>
        public static Dictionary<string, ItemText> ParseTexts(string json, List<string> errors)
        {
            try
            {
                var texts = JsonConvert.DeserializeObject<Dictionary<string, ItemText>>(json);
                if (texts != null)
                    return texts;
                errors.Add($"{TextsPath} が空です。");
            }
            catch (JsonException exception)
            {
                errors.Add($"{TextsPath} の書式が正しくありません: {exception.Message}");
            }
            return null;
        }

        /// <summary>画像の key と items.json の key が1対1で、名前が空でないことを確かめる。</summary>
        public static List<string> ValidateTexts(
            IEnumerable<string> imageKeys,
            IReadOnlyDictionary<string, ItemText> texts
        )
        {
            var errors = new List<string>();
            var keys = imageKeys.ToHashSet();
            foreach (
                string key in keys.Where(key => !texts.ContainsKey(key))
                    .OrderBy(k => k, StringComparer.Ordinal)
            )
                errors.Add($"items.json に key={key} がありません(画像はあります)。");
            foreach (
                string key in texts
                    .Keys.Where(key => !keys.Contains(key))
                    .OrderBy(k => k, StringComparer.Ordinal)
            )
                errors.Add($"items.json の key={key} に対応する画像がありません。");
            foreach (
                var pair in texts.Where(pair =>
                    keys.Contains(pair.Key) && string.IsNullOrWhiteSpace(pair.Value?.name)
                )
            )
                errors.Add($"items.json の key={pair.Key} の name が空です。");
            return errors;
        }

        /// <summary>ファイル名の名前部分(ハイフン区切り)を key(snake_case)にする。</summary>
        public static string ToKey(string namePart) => namePart.Replace('-', '_');

        /// <summary>key をサブアセット名(PascalCase)にする。例: fluorescent_light → FluorescentLight</summary>
        public static string ToAssetName(string key) =>
            string.Concat(
                key.Split('_')
                    .Where(word => word.Length > 0)
                    .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1))
            );

        private static List<Entry> CollectEntries(List<string> errors)
        {
            var entries = new List<Entry>();
            foreach (Group group in Groups)
            {
                if (!AssetDatabase.IsValidFolder(group.ImageDir))
                {
                    errors.Add($"画像フォルダがありません: {group.ImageDir}");
                    continue;
                }

                foreach (string path in FindImages(group.ImageDir))
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string[] names = ParseFileName(fileName, group.Prefix, group.PostCraft);
                    if (names == null)
                    {
                        string expected = group.PostCraft ? "<素材1>_<素材2>" : "<名前>";
                        errors.Add(
                            $"ファイル名の形式が違います: {path}(期待: {group.Prefix}{expected}.png、名前は英小文字とハイフン)"
                        );
                        continue;
                    }

                    entries.Add(
                        new Entry(
                            group,
                            path,
                            string.Join("_", names),
                            group.PostCraft ? names : null
                        )
                    );
                }
            }

            foreach (var duplicate in entries.GroupBy(entry => entry.Key).Where(g => g.Count() > 1))
                errors.Add(
                    $"key={duplicate.Key} が重複しています: "
                        + string.Join(", ", duplicate.Select(entry => entry.ImagePath))
                );

            foreach (Entry entry in entries.Where(entry => entry.MaterialKeys != null))
            {
                if (entry.MaterialKeys[0] == entry.MaterialKeys[1])
                    errors.Add($"同じ素材は2つ指定できません: {entry.ImagePath}");

                foreach (string materialKey in entry.MaterialKeys)
                {
                    bool exists = entries.Any(other =>
                        other.MaterialKeys == null
                        && other.Group.Category == entry.Group.Category
                        && other.Key == materialKey
                    );
                    if (!exists)
                        errors.Add(
                            $"素材 {materialKey} の合成前の画像がありません: {entry.ImagePath}"
                        );
                }
            }

            foreach (
                var duplicate in entries
                    .Where(entry => entry.MaterialKeys != null)
                    .GroupBy(entry =>
                        string.Join("|", entry.MaterialKeys.OrderBy(k => k, StringComparer.Ordinal))
                    )
                    .Where(g => g.Count() > 1)
            )
                errors.Add(
                    "同じ素材の組み合わせが重複しています: "
                        + string.Join(", ", duplicate.Select(entry => entry.ImagePath))
                );

            return entries;
        }

        private static IEnumerable<string> FindImages(string folder) =>
            AssetDatabase
                .FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    Path.GetDirectoryName(path)?.Replace('\\', '/') == folder
                    && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(path => path, StringComparer.Ordinal);

        private static int NextId(Group group, IEnumerable<Entry> entries)
        {
            var used = entries
                .Where(entry => entry.Group == group && entry.Item != null)
                .Select(entry => entry.Item.id)
                .ToList();
            return used.Count > 0 ? used.Max() + 1 : group.FirstId;
        }

        private static void Apply(
            Entry entry,
            IReadOnlyDictionary<string, ItemData> itemByKey,
            ItemText text
        )
        {
            ItemData item = entry.Item;
            Undo.RecordObject(item, "アイテムを同期");
            ConfigureSprite(entry.ImagePath);
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(entry.ImagePath);
            item.key = entry.Key;
            item.category = entry.Group.Category;
            item.name = ToAssetName(entry.Key);
            item.itemName = text.name;
            item.description = text.description ?? string.Empty;

            if (entry.MaterialKeys == null)
            {
                item.material1 = null;
                item.material2 = null;
            }
            else
            {
                item.material1 = itemByKey[entry.MaterialKeys[0]];
                item.material2 = itemByKey[entry.MaterialKeys[1]];
                ApplyPostCraft(item);
            }

            EditorUtility.SetDirty(item);
        }

        private static void ApplyPostCraft(ItemData item)
        {
            if (item is FoodData food)
            {
                var serialized = new SerializedObject(food);
                serialized.FindProperty("_craftedResult").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>アイコンのインポート設定をそろえる。最大表示(4Kの入手演出で512px)に足りる512に縮めて圧縮する。</summary>
        private static void ConfigureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (
                importer == null
                || (
                    importer.textureType == TextureImporterType.Sprite
                    && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.maxTextureSize == IconMaxSize
                    && importer.textureCompression == TextureImporterCompression.Compressed
                    && importer.mipmapEnabled
                )
            )
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = IconMaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        /// <summary>items.json の1件分。</summary>
        public sealed class ItemText
        {
            public string name;
            public string description;
        }

        private sealed class Group
        {
            public string ImageDir { get; }
            public string Prefix { get; }
            public Type ItemType { get; }
            public ItemCategory Category { get; }
            public bool PostCraft { get; }
            public int FirstId { get; }

            public Group(
                string imageDir,
                string prefix,
                Type itemType,
                ItemCategory category,
                bool postCraft,
                int firstId
            )
            {
                ImageDir = imageDir;
                Prefix = prefix;
                ItemType = itemType;
                Category = category;
                PostCraft = postCraft;
                FirstId = firstId;
            }
        }

        private sealed class Entry
        {
            public Group Group { get; }
            public string ImagePath { get; }
            public string Key { get; }
            public string[] MaterialKeys { get; }
            public ItemData Item { get; set; }

            public Entry(Group group, string imagePath, string key, string[] materialKeys)
            {
                Group = group;
                ImagePath = imagePath;
                Key = key;
                MaterialKeys = materialKeys;
            }
        }
    }
}
