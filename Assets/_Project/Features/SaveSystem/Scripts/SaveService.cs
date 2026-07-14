using System.Collections.Generic;
using System.IO;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 進行度・フラグ(ProgressManager)と所持品(InventoryManager)を1ファイルに全書き/復元する。
    /// マニュアルセーブ専用(セーブUIの「はい」から Save を呼ぶ)。単一スロット上書き。spec §6。
    /// </summary>
    public static class SaveService
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static bool HasSave() => File.Exists(FilePath);

        /// <summary>現在の進行度・フラグ・所持品をディスクへ全書きする。</summary>
        public static void Save()
        {
            var data = new SaveData();

            var pm = ProgressManager.Instance;
            if (pm != null)
            {
                data.progress = pm.Progress;
                foreach (var kv in pm.Flags)
                    data.flags.Add(new FlagEntry { key = kv.Key, value = kv.Value });
            }

            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                foreach (var stack in inv.GetAllItems())
                {
                    if (stack?.Data == null)
                        continue;
                    data.items.Add(
                        new ItemEntry
                        {
                            itemId = stack.Data.id,
                            count = stack.Count,
                            equipped = stack.IsEquipped,
                            rolledStats =
                                stack.RolledStats != null
                                    ? new List<RolledStat>(stack.RolledStats)
                                    : null,
                        }
                    );
                }
            }

            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveService] 保存しました: {FilePath}");
        }

        /// <summary>ディスクから復元する。セーブが無ければ false。</summary>
        public static bool Load()
        {
            if (!HasSave())
                return false;

            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data == null)
                return false;

            var pm = ProgressManager.Instance;
            if (pm != null)
            {
                var flags = new Dictionary<string, string>();
                if (data.flags != null)
                {
                    foreach (var f in data.flags)
                        if (!string.IsNullOrEmpty(f?.key))
                            flags[f.key] = f.value;
                }
                pm.LoadState(data.progress, flags);
            }

            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.Clear();
                var db = ItemDB.Instance;
                if (data.items != null && db != null)
                    RestoreItems(inv, db, data.items);
            }

            Debug.Log($"[SaveService] 復元しました: {FilePath}");
            return true;
        }

        private static void RestoreItems(InventoryManager inv, ItemDB db, List<ItemEntry> entries)
        {
            foreach (var e in entries)
            {
                var itemData = db.GetItemById(e.itemId);
                if (itemData == null)
                {
                    Debug.LogWarning(
                        $"[SaveService] itemId {e.itemId} は ItemDB に無し。スキップ。"
                    );
                    continue;
                }

                if (e.rolledStats != null && e.rolledStats.Count > 0)
                {
                    var stack = inv.AddInstance(itemData, e.rolledStats);
                    if (stack != null)
                        stack.IsEquipped = e.equipped;
                }
                else
                {
                    inv.AddItem(itemData, e.count);
                    if (e.equipped)
                    {
                        var restored = inv.GetAllItems()
                            .Find(s => s.Data == itemData && !s.IsInstance);
                        if (restored != null)
                            restored.IsEquipped = true;
                    }
                }
            }
        }
    }
}
