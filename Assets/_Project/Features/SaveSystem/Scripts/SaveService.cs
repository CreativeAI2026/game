using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セーブ。進行度・フラグ(ProgressManager)・所持品(InventoryManager)・装備(EquipmentSlots)・
    /// 即時食材の枠(QuickFoodSlots)・拾い済みのフィールドアイテム(FieldItemManager)・プレイヤー状態(現在HP・座標・向き)・武器を
    /// <see cref="SaveData"/> にまとめて1ファイルに書く。読み戻すのは <see cref="LoadService"/>。
    /// マニュアルセーブ専用(セーブUIの「はい」から Save を呼ぶ)。単一スロット上書き。
    /// </summary>
    public static class SaveService
    {
        public static void Save()
        {
            // セーブ可能なのは「フィールド移動中」だけ(戦闘モード中・会話イベント再生中は不可)。
            // 入口(HudIconBarView)側でもボタンを塞いでいるが、別経路からの呼び出しに備えた多重防御。
            if (!CanSaveNow(out string blockedReason))
            {
                Debug.LogWarning(
                    $"[SaveService] セーブを中断しました({blockedReason})。フィールド移動中のみセーブ可能です。"
                );
                return;
            }

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
                var quickFoodSlots =
                    InventoryManager.CurrentQuickFood != null
                        ? InventoryManager.CurrentQuickFood.GetSlots()
                        : System.Array.Empty<ItemStack>();
                var equipmentSlots =
                    InventoryManager.CurrentEquipment != null
                        ? InventoryManager.CurrentEquipment.GetSlots()
                        : System.Array.Empty<ItemStack>();
                foreach (var stack in inv.GetAllItems())
                {
                    if (stack?.Data == null)
                        continue;
                    int quickSlot = IndexOfSlot(quickFoodSlots, stack);
                    int equipmentSlot = IndexOfSlot(equipmentSlots, stack);
                    data.items.Add(
                        new ItemEntry
                        {
                            itemId = stack.Data.id,
                            itemKey = stack.Data.key,
                            count = stack.Count,
                            equipped = equipmentSlot >= 0,
                            equipmentSlot = equipmentSlot >= 0 ? equipmentSlot : 0,
                            rolledStats =
                                stack.RolledStats != null
                                    ? new List<RolledStat>(stack.RolledStats)
                                    : null,
                            inQuickFood = quickSlot >= 0,
                            quickFoodSlot = quickSlot >= 0 ? quickSlot : 0,
                        }
                    );
                }
            }

            var fieldItems = FieldItemManager.Instance;
            if (fieldItems != null)
                data.pickedItemIds.AddRange(fieldItems.Snapshot());

            // 入手ずみ武器と選択武器。WeaponManager が無ければ「0本・未選択」。
            var weapons = WeaponManager.Instance;
            data.selectedWeaponIndex =
                weapons != null ? weapons.CurrentWeaponIndex : WeaponManager.NoWeapon;
            if (weapons != null)
                data.ownedWeaponKeys.AddRange(weapons.CaptureOwnedWeaponKeys());

            CapturePlayer(data);

            SaveFile.Write(JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveService] 保存しました: {SaveFile.FilePath}");
        }

        /// <summary>
        /// 現在セーブしてよいか(探索中。そこから開いたセーブ画面などのメニュー中も含む)。戦闘中・会話イベント再生中は false。
        /// マネージャ未生成(タイトル直後など)は Field 扱いで許可する。
        /// </summary>
        private static bool CanSaveNow(out string blockedReason)
        {
            var mode = GameModeManager.Current;
            blockedReason = mode switch
            {
                GameMode.Battle => "戦闘モード中",
                GameMode.Dialogue => "会話イベント再生中",
                _ => null,
            };
            return mode == GameMode.Field;
        }

        /// <summary>今のプレイヤーから座標・向き・現在HPを取り込む。リグ未生成なら hasPlayerState=false。</summary>
        private static void CapturePlayer(SaveData data)
        {
            var player = PlayerRegistry.Current;
            if (player == null)
                return;

            var pos = player.transform.position;
            data.posX = pos.x;
            data.posY = pos.y;
            data.posZ = pos.z;
            data.rotationY = player.transform.eulerAngles.y;

            // 現在HPは PlayerStatus から取る。無ければ座標だけ保存。
            var status = player.GetComponentInChildren<PlayerStatus>();
            data.currentHp = status != null ? status.CaptureHp() : 0f;

            data.hasPlayerState = true;
        }

        /// <summary>stack が入っている枠の番号を返す。無ければ -1。</summary>
        private static int IndexOfSlot(IReadOnlyList<ItemStack> slots, ItemStack stack)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == stack)
                    return i;
            return -1;
        }
    }
}
