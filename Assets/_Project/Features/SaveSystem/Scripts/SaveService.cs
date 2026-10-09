using System.Collections.Generic;
using System.IO;
using CreativeAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 進行度・フラグ(ProgressManager)・所持品(InventoryManager)・プレイヤー状態(現在HP・座標・シーン)を
    /// 1ファイルに全書き/復元する。マニュアルセーブ専用(セーブUIの「はい」から Save を呼ぶ)。単一スロット上書き。
    /// </summary>
    public static class SaveService
    {
        private const string PlayerTag = "Player";

        private static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static bool HasSave() => File.Exists(FilePath);

        /// <summary>現在の進行度・フラグ・所持品をディスクへ全書きする。</summary>
        public static void Save()
        {
            // セーブ可能なのは「フィールド移動中」だけ(戦闘モード中・会話イベント再生中は不可)。
            // 入口(HudIconBar)側でもボタンを塞いでいるが、別経路からの呼び出しに備えた多重防御。
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
                var quickFoodSlots = inv.GetQuickFoodSlots();
                foreach (var stack in inv.GetAllItems())
                {
                    if (stack?.Data == null)
                        continue;
                    int quickSlot = IndexOfQuickFoodSlot(quickFoodSlots, stack);
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
                            inQuickFood = quickSlot >= 0,
                            quickFoodSlot = quickSlot >= 0 ? quickSlot : 0,
                        }
                    );
                }
            }

            CapturePlayer(data);

            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveService] 保存しました: {FilePath}");
        }

        /// <summary>
        /// 現在セーブしてよいか(フィールド移動中のみ)。戦闘モード中・会話イベント再生中は false。
        /// マネージャ未生成(タイトル直後など)は Field 扱いで許可する。
        /// </summary>
        private static bool CanSaveNow(out string blockedReason)
        {
            var mode = GameModeManager.Instance;
            if (mode != null && mode.CurrentMode != GameMode.Field)
            {
                blockedReason = "戦闘モード中";
                return false;
            }
            if (EventPlaybackService.IsPlaying)
            {
                blockedReason = "会話イベント再生中";
                return false;
            }
            blockedReason = null;
            return true;
        }

        /// <summary>tag=Player のリグ root から座標・向き・現在HP・現在シーンを取り込む。リグ未生成なら hasPlayerState=false。</summary>
        private static void CapturePlayer(SaveData data)
        {
            var player = GameObject.FindWithTag(PlayerTag);
            if (player == null)
                return;

            data.sceneName = SceneManager.GetActiveScene().name;
            var pos = player.transform.position;
            data.posX = pos.x;
            data.posY = pos.y;
            data.posZ = pos.z;
            data.rotationY = player.transform.eulerAngles.y;

            // 現在HPは PlayerStatus から取る。無ければ座標だけ保存。
            var status = player.GetComponentInChildren<PlayerStatus>();
            data.currentHp = status != null ? status.CaptureHp() : 0f;

            // 入手ずみ武器と選択武器を保存。WeaponManager が無ければ「0本・未選択」。
            var weapon = player.GetComponentInChildren<WeaponManager>();
            data.selectedWeaponIndex = weapon != null ? weapon.CaptureSelectedWeaponIndex() : -1;
            data.ownedWeaponKeys.Clear();
            if (weapon != null)
            {
                var owned = weapon.CaptureOwnedWeaponKeys();
                if (owned != null)
                    data.ownedWeaponKeys.AddRange(owned);
            }

            data.hasPlayerState = true;
        }

        /// <summary>
        /// ディスクから進行度・フラグ・所持品を復元し、読み込んだ SaveData を返す(セーブが無ければ null)。
        /// プレイヤーの座標・HP は対象シーンのロード後でないと配置できないため、ここでは復元しない。
        /// 呼び出し側は戻り値の sceneName でシーンをロードし、その完了時に <see cref="RestorePlayerState"/> を呼ぶ。
        /// </summary>
        public static SaveData Load()
        {
            if (!HasSave())
                return null;

            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
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
            {
                inv.Clear();
                var db = ItemDB.Instance;
                if (data.items != null && db != null)
                    RestoreItems(inv, db, data.items);
            }

            Debug.Log($"[SaveService] 復元しました: {FilePath}");
            return data;
        }

        /// <summary>
        /// 対象シーンのロード完了後に、tag=Player のリグを保存座標・向きへ移し、現在HPを復元する。
        /// 所持品(装備)の復元が済んだ後に呼ぶこと(最大HPが確定してから HP をクランプするため)。
        /// </summary>
        public static void RestorePlayerState(SaveData data)
        {
            if (data == null || !data.hasPlayerState)
                return;

            var player = GameObject.FindWithTag(PlayerTag);
            if (player == null)
                return;

            player.transform.SetPositionAndRotation(
                new Vector3(data.posX, data.posY, data.posZ),
                Quaternion.Euler(0f, data.rotationY, 0f)
            );

            // 武器を先に復元して最終ステータス(最大HP等)を確定させてから HP をクランプする。
            var weapon = player.GetComponentInChildren<WeaponManager>();
            if (weapon != null)
                weapon.RestoreWeapons(data.ownedWeaponKeys, data.selectedWeaponIndex);

            var status = player.GetComponentInChildren<PlayerStatus>();
            if (status != null)
                status.RestoreHp(data.currentHp);
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

                ItemStack stack;
                if (e.rolledStats != null && e.rolledStats.Count > 0)
                {
                    stack = inv.AddInstance(itemData, e.rolledStats);
                    if (stack != null)
                        stack.IsEquipped = e.equipped;
                }
                else
                {
                    inv.AddItem(itemData, e.count);
                    stack = inv.GetAllItems().Find(s => s.Data == itemData && !s.IsInstance);
                    if (stack != null && e.equipped)
                        stack.IsEquipped = true;
                }

                // 即時使用食材スロットの復元(食材のみ・SetQuickFood 側で食材/在庫を検証)。
                if (stack != null && e.inQuickFood)
                    inv.SetQuickFood(e.quickFoodSlot, stack);
            }
        }

        /// <summary>stack が入っている即時使用食材スロット番号を返す。未セットは -1。</summary>
        private static int IndexOfQuickFoodSlot(IReadOnlyList<ItemStack> slots, ItemStack stack)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == stack)
                    return i;
            return -1;
        }
    }

    /// <summary>
    /// マニュアルセーブでディスクに全書きするスナップショット(単一スロット)。
    /// JsonUtility でシリアライズするため public フィールド + [Serializable] で構成する。
    /// 保存はマニュアルセーブ時のみ・オートセーブなし。
    /// </summary>
    [System.Serializable]
    public sealed class SaveData
    {
        public int progress;
        public List<FlagEntry> flags = new();
        public List<ItemEntry> items = new();

        // プレイヤー状態(現在HP・座標を保存 → 死亡時は直近セーブから再開)。
        // hasPlayerState=false の場合(リグ未実装・旧セーブ)は復元をスキップし、既定スポーン/満タンで開始する。
        public bool hasPlayerState;

        /// <summary>再開時に読み込むフィールドシーン名。空なら呼び出し側の既定シーンにフォールバック。</summary>
        public string sceneName;
        public float posX;
        public float posY;
        public float posZ;

        /// <summary>向き(Y軸回転, 度)。座標だけでは向きが失われるため保持する。</summary>
        public float rotationY;

        /// <summary>保存時点の現在HP。実体は PlayerStatus から取得する。</summary>
        public float currentHp;

        /// <summary>
        /// 選択中の武器 index(プレイヤーリグは選択武器も保存)。実体は WeaponManager。
        /// <see cref="ownedWeaponKeys"/> に含まれない値なら所持の先頭へ寄せて復元される。
        /// </summary>
        public int selectedWeaponIndex;

        /// <summary>
        /// 入手ずみ武器のキー(sword/bow/scythe)。主人公は初期0本でイベント入手なので、
        /// 「どれを持っているか」自体がセーブ対象になる。旧セーブに無ければ空 = 0本で復元される。
        /// </summary>
        public List<string> ownedWeaponKeys = new();
    }

    [System.Serializable]
    public sealed class FlagEntry
    {
        public string key;
        public string value;
    }

    /// <summary>所持品1件。ロール済み個体は rolledStats を持ち、スタック品は空。</summary>
    [System.Serializable]
    public sealed class ItemEntry
    {
        public int itemId;
        public int count;
        public bool equipped;
        public List<RolledStat> rolledStats;

        /// <summary>
        /// このスタックが即時使用食材スロットにセットされているか。旧セーブは既定 false(未セット)で復元される。
        /// bool にしているのは、JsonUtility が欠落フィールドに初期化子(-1 等)を反映しない場合でも
        /// 既定 false で安全に「未セット」と判定するため(slot=0 との誤判定を避ける)。
        /// </summary>
        public bool inQuickFood;

        /// <summary>即時使用食材スロット番号(0..2)。<see cref="inQuickFood"/> が true のときのみ有効。</summary>
        public int quickFoodSlot;
    }
}
