using System;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// マニュアルセーブでディスクに全書きするスナップショット(単一スロット)。
    /// JsonUtility でシリアライズするため public フィールド + [Serializable] で構成する。
    /// spec §6: 保存はマニュアルセーブ時のみ・オートセーブなし。
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int progress;
        public List<FlagEntry> flags = new();
        public List<ItemEntry> items = new();

        // プレイヤー状態(spec §6 図: 現在HP・座標を保存 → 死亡時は直近セーブから再開)。
        // hasPlayerState=false の場合(リグ未実装・旧セーブ)は復元をスキップし、既定スポーン/満タンで開始する。
        public bool hasPlayerState;

        /// <summary>再開時に読み込むフィールドシーン名。空なら呼び出し側の既定シーンにフォールバック。</summary>
        public string sceneName;
        public float posX;
        public float posY;
        public float posZ;

        /// <summary>向き(Y軸回転, 度)。座標だけでは向きが失われるため保持する。</summary>
        public float rotationY;

        /// <summary>保存時点の現在HP。実体は担当班の PlayerStatus(ISaveableActor)から取得する。</summary>
        public float currentHp;
    }

    [Serializable]
    public sealed class FlagEntry
    {
        public string key;
        public string value;
    }

    /// <summary>所持品1件。ロール済み個体は rolledStats を持ち、スタック品は空。</summary>
    [Serializable]
    public sealed class ItemEntry
    {
        public int itemId;
        public int count;
        public bool equipped;
        public List<RolledStat> rolledStats;
    }
}
