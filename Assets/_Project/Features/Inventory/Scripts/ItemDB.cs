using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    /// <summary>CSVの1レコードを解析する。カンマを含む引用フィールドと二重引用符を扱う。</summary>
    public static class CsvRecordParser
    {
        public static IReadOnlyList<string> Parse(string line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            var columns = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else if (quoted)
                        quoted = false;
                    else if (value.Length == 0)
                        quoted = true;
                    else
                        throw new FormatException("引用符はフィールドの先頭に置いてください。");
                }
                else if (current == ',' && !quoted)
                {
                    columns.Add(value.ToString());
                    value.Clear();
                }
                else
                    value.Append(current);
            }

            if (quoted)
                throw new FormatException("引用符が閉じられていません。");

            columns.Add(value.ToString());
            return columns;
        }
    }

    [CreateAssetMenu(fileName = "ItemDB", menuName = "Scriptable Objects/ItemDB")]
    public class ItemDB : ScriptableObject
    {
        private const string InventoryDataFolder = "Assets/_Project/Features/Inventory/Data";

        private static ItemDB _instance;
        private static ItemDB _injected;

        public static ItemDB Instance
        {
            get
            {
                // 注入中は Resources ロードも同期もしない(合成カタログが実アセットで潰れるため)。
                if (_injected != null)
                    return _injected;
                _instance ??= Resources.Load<ItemDB>("ItemDB");
#if UNITY_EDITOR
                _instance?.SyncFromInventoryDataFolder();
#endif
                return _instance;
            }
        }

        /// <summary>
        /// テスト専用: <see cref="Instance"/> を合成カタログに差し替える。null を渡すと解除して通常のロードに戻る。
        /// ItemDB は Resources + Data フォルダ同期で自分を組み立てるため、実アセットに依存せず
        /// itemKey→ItemData の解決を検証したいテストにはこの注入口が要る。
        /// </summary>
        public static void InjectForTests(IReadOnlyList<ItemData> testItems)
        {
            if (_injected != null)
                DestroyImmediate(_injected);
            _injected = null;
            if (testItems == null)
                return;

            _injected = CreateInstance<ItemDB>();
            _injected.items = testItems.Where(i => i != null).ToList();
        }

        [SerializeField, HideInInspector]
        private List<ItemData> items = new();

        public IReadOnlyList<ItemData> Items
        {
            get
            {
#if UNITY_EDITOR
                SyncFromInventoryDataFolder();
#endif
                return items;
            }
        }

        public ItemData GetItemById(int id)
        {
            if (items == null)
                return null;
            return items.FirstOrDefault(i => i != null && i.id == id);
        }

        /// <summary>events.json の giveItem/itemKey(文字列)から ItemData を引く。未設定・未一致は null。</summary>
        public ItemData GetItemByKey(string key)
        {
            if (string.IsNullOrEmpty(key) || items == null)
                return null;
            return items.FirstOrDefault(i => i != null && i.key == key);
        }

#if UNITY_EDITOR
        [ContextMenu("Sync From Inventory Data Folder")]
        public void SyncFromInventoryDataFolder()
        {
            // テスト注入インスタンスは実アセットで上書きしない。
            if (this == _injected)
                return;

            var loadedItems = AssetDatabase
                .FindAssets("t:ItemData", new[] { InventoryDataFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                .Where(item => item != null)
                .ToList();

            if (items != null && items.SequenceEqual(loadedItems))
                return;

            Undo.RecordObject(this, "Sync ItemDB");
            items = loadedItems;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
