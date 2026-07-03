using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "ItemDB", menuName = "Scriptable Objects/ItemDB")]
    public class ItemDB : ScriptableObject
    {
        private const string InventoryDataFolder = "Assets/_Project/Features/Inventory/Data";

        private static ItemDB _instance;
        public static ItemDB Instance
        {
            get
            {
                _instance ??= Resources.Load<ItemDB>("ItemDB");
#if UNITY_EDITOR
                _instance?.SyncFromInventoryDataFolder();
#endif
                return _instance;
            }
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

#if UNITY_EDITOR
        [ContextMenu("Sync From Inventory Data Folder")]
        public void SyncFromInventoryDataFolder()
        {
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
