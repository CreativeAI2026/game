using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// マニュアルセーブでディスクに全書きするスナップショット(単一スロット)。<see cref="SaveService"/> が書き、<see cref="LoadService"/> が戻す。
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

        public float posX;
        public float posY;
        public float posZ;

        /// <summary>向き(Y軸回転, 度)。座標だけでは向きが失われるため保持する。</summary>
        public float rotationY;

        /// <summary>保存時点の現在HP。実体は PlayerStatus から取得する。</summary>
        public float currentHp;

        /// <summary>
        /// 選択中の武器 index。実体は WeaponManager。
        /// <see cref="ownedWeaponKeys"/> に含まれない値なら所持の先頭へ寄せて復元される。
        /// </summary>
        public int selectedWeaponIndex;

        /// <summary>
        /// 入手ずみ武器のキー(sword/bow/scythe/hammer)。主人公は初期0本でイベント入手なので、
        /// 「どれを持っているか」自体がセーブ対象になる。旧セーブに無ければ空 = 0本で復元される。
        /// </summary>
        public List<string> ownedWeaponKeys = new();

        /// <summary>
        /// フィールドで拾い済みのアイテムの識別子(<see cref="FieldItemPickup.PickupId"/>)。
        /// 旧セーブに無ければ空 = 全部まだ拾っていない扱いで復元される。
        /// </summary>
        public List<string> pickedItemIds = new();
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

        /// <summary>保存時のアイテムキー。復元時は数値IDより優先し、番号の再利用による取り違えを防ぐ。</summary>
        public string itemKey;
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

        /// <summary>装備の枠番号(0..2)。<see cref="equipped"/> が true のときのみ有効。旧セーブは 0 で読まれる。</summary>
        public int equipmentSlot;
    }
}
