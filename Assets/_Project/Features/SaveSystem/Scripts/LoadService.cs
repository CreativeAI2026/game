using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ロード。<see cref="SaveService"/> が書いた <see cref="SaveData"/> を読み、進行度・フラグ・所持品・装備と食材の枠・
    /// 拾い済みのフィールドアイテムを戻す(<see cref="Load"/>)。プレイヤー状態は対象シーンを読み込んでから戻す(<see cref="RestorePlayerState"/>)。
    /// </summary>
    public static class LoadService
    {
        /// <summary>読み込めるセーブ(本体か予備)があるか。</summary>
        public static bool HasSave() => SaveFile.Exists();

        /// <summary>
        /// ディスクから進行度・フラグ・所持品・武器を復元し、読み込んだ SaveData を返す(セーブが無いか、本体も予備も壊れていれば null)。
        /// プレイヤーの座標・HP は対象シーンのロード後でないと配置できないため、ここでは復元しない。
        /// 呼び出し側はフィールドのシーンをロードし、その完了時に <see cref="RestorePlayerState"/> を呼ぶ。
        /// </summary>
        public static SaveData Load()
        {
            var data = SaveFile.Read();
            if (data == null)
                return null;

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
                RestoreItems(inv, ItemCatalog.Instance, data.items);

            // セーブ後に拾ったアイテムは所持品から消えるので、拾った記録もセーブ時点に戻して
            // フィールドにまた置かれるようにする(旧セーブは項目が無い = 空)。
            var fieldItems = FieldItemManager.Instance;
            if (fieldItems != null)
                fieldItems.Restore(data.pickedItemIds);

            // 旧セーブに武器の項目が無ければ 0本で戻る。
            var weapons = WeaponManager.Instance;
            if (weapons != null)
                weapons.RestoreWeapons(data.ownedWeaponKeys, data.selectedWeaponIndex);

            Debug.Log($"[LoadService] 復元しました: {SaveFile.FilePath}");
            return data;
        }

        /// <summary>
        /// 対象シーンのロード完了後に、今のプレイヤーを保存座標・向きへ移し、現在HPを復元する。
        /// 所持品(装備)と武器の復元(Load)が済んだ後に呼ぶこと(最大HPが確定してから HP をクランプするため)。
        /// </summary>
        public static void RestorePlayerState(SaveData data)
        {
            if (data == null || !data.hasPlayerState)
                return;

            var player = PlayerRegistry.Current;
            if (player == null)
                return;

            SpawnPoint.PlaceAt(
                player.gameObject,
                new Vector3(data.posX, data.posY, data.posZ),
                Quaternion.Euler(0f, data.rotationY, 0f)
            );

            // 武器は Load で常駐に戻してあるので、最終ステータス(最大HP等)は確定している。
            var status = player.GetComponentInChildren<PlayerStatus>();
            if (status != null)
                status.RestoreHp(data.currentHp);
        }

        // セーブの1件ずつを束に戻し、装備・即時食材の枠も組み立ててから、所持品へまとめて差し替える
        // (1件ずつ足すと、通知を受けた側が「所持品はあるのに装備がまだ」の途中の状態を見てしまうため)。
        private static void RestoreItems(
            InventoryManager inv,
            ItemCatalog catalog,
            List<ItemEntry> entries
        )
        {
            var stacks = new List<ItemStack>();
            var equipment = new ItemStack[EquipmentSlots.SlotCount];
            var quickFood = new ItemStack[QuickFoodSlots.SlotCount];
            foreach (var e in entries ?? new List<ItemEntry>())
            {
                var itemData = catalog != null ? ResolveSavedItem(catalog, e) : null;
                if (itemData == null)
                {
                    Debug.LogWarning(
                        $"[LoadService] itemKey '{e.itemKey}' / itemId {e.itemId} は ItemCatalog に無し。スキップ。"
                    );
                    continue;
                }

                var stack =
                    e.rolledStats != null && e.rolledStats.Count > 0
                        ? new ItemStack(itemData, e.rolledStats)
                        : new ItemStack(itemData, e.count);
                stacks.Add(stack);

                if (e.equipped)
                {
                    int slot = RestoredSlot(equipment, e.equipmentSlot);
                    if (slot >= 0)
                        equipment[slot] = stack;
                }
                // 即時使用食材の枠(食材か・所持品にあるかは InventoryManager.Restore 側で確かめる)。
                if (e.inQuickFood && e.quickFoodSlot >= 0 && e.quickFoodSlot < quickFood.Length)
                    quickFood[e.quickFoodSlot] = stack;
            }

            inv.Restore(stacks, equipment, quickFood);
        }

        // キーを持たない旧セーブは旧装備の意味で解決し、同じ番号の新装備へ置き換えない。
        private static ItemData ResolveSavedItem(ItemCatalog catalog, ItemEntry entry)
        {
            string key = entry.itemKey;
            if (string.IsNullOrEmpty(key))
                key = LegacyEquipmentKey(entry.itemId);
            return string.IsNullOrEmpty(key)
                ? catalog.GetItemById(entry.itemId)
                : catalog.GetItemByKey(key);
        }

        // 装備素材が10種類だった時点のID。削除済みのキーは復元先なしとして扱う。
        private static string LegacyEquipmentKey(int id) =>
            id switch
            {
                2001 => "balloon",
                2002 => "clock",
                2003 => "microphone",
                2004 => "note",
                2006 => "fluorescent_light",
                2007 => "full_body_tights",
                2008 => "glasses",
                2009 => "hat",
                2010 => "magnet",
                2011 => "umbrella",
                2101 => "balloon_clock",
                2102 => "balloon_fluorescent_light",
                2103 => "balloon_full_body_tights",
                2104 => "balloon_glasses",
                2105 => "balloon_hat",
                2106 => "balloon_magnet",
                2107 => "balloon_microphone",
                2108 => "balloon_note",
                2109 => "balloon_umbrella",
                2110 => "clock_fluorescent_light",
                2111 => "clock_full_body_tights",
                2112 => "clock_glasses",
                2113 => "clock_hat",
                2114 => "clock_magnet",
                2115 => "clock_microphone",
                2116 => "clock_note",
                2117 => "clock_umbrella",
                2118 => "fluorescent_light_full_body_tights",
                2119 => "fluorescent_light_glasses",
                2120 => "fluorescent_light_hat",
                2121 => "fluorescent_light_magnet",
                2122 => "fluorescent_light_microphone",
                2123 => "fluorescent_light_note",
                2124 => "fluorescent_light_umbrella",
                2125 => "full_body_tights_glasses",
                2126 => "full_body_tights_hat",
                2127 => "full_body_tights_magnet",
                2128 => "full_body_tights_microphone",
                2129 => "full_body_tights_note",
                2130 => "full_body_tights_umbrella",
                2131 => "glasses_hat",
                2132 => "glasses_magnet",
                2133 => "glasses_microphone",
                2134 => "glasses_note",
                2135 => "glasses_umbrella",
                2136 => "hat_magnet",
                2137 => "hat_microphone",
                2138 => "hat_note",
                2139 => "hat_umbrella",
                2140 => "magnet_microphone",
                2141 => "magnet_note",
                2142 => "magnet_umbrella",
                2143 => "microphone_note",
                2144 => "microphone_umbrella",
                2145 => "note_umbrella",
                _ => null,
            };

        /// <summary>
        /// 装備を戻す枠。保存した枠が埋まっている・範囲外なら最初の空き枠にする
        /// (枠番号を持たない旧セーブはすべて 0 で読まれるため、空いている枠へ順に詰める)。空きが無ければ -1。
        /// </summary>
        private static int RestoredSlot(ItemStack[] slots, int saved)
        {
            if (saved >= 0 && saved < slots.Length && slots[saved] == null)
                return saved;
            return System.Array.IndexOf(slots, null);
        }
    }
}
