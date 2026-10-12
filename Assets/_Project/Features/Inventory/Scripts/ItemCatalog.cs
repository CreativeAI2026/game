using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Scriptable Objects/ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        private const string InventoryDataFolder = "Assets/_Project/Features/Inventory/Data";

        private static ItemCatalog _instance;
        private static ItemCatalog _injected;

        public static ItemCatalog Instance
        {
            get
            {
                // 注入中は Resources ロードも同期もしない(合成カタログが実アセットで潰れるため)。
                if (_injected != null)
                    return _injected;
                _instance ??= Resources.Load<ItemCatalog>("ItemCatalog");
#if UNITY_EDITOR
                _instance?.SyncFromInventoryDataFolder();
#endif
                return _instance;
            }
        }

        /// <summary>
        /// テスト専用: <see cref="Instance"/> を合成カタログに差し替える。null を渡すと解除して通常のロードに戻る。
        /// ItemCatalog は Resources + Data フォルダ同期で自分を組み立てるため、実アセットに依存せず
        /// itemKey→ItemData の解決を検証したいテストにはこの注入口が要る。
        /// </summary>
        public static void InjectForTests(IReadOnlyList<ItemData> testItems)
        {
            if (_injected != null)
                DestroyImmediate(_injected);
            _injected = null;
            if (testItems == null)
                return;

            _injected = CreateInstance<ItemCatalog>();
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

            Undo.RecordObject(this, "Sync ItemCatalog");
            items = loadedItems;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
