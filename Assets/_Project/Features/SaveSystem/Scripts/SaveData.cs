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
