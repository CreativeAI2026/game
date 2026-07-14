using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public enum ItemCategory
    {
        Weapon,
        Equipment,
        Food,
        Important,
    }

    [CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
    public class ItemData : ScriptableObject
    {
        public Sprite icon; // アイコン
        public int id; // ID
        public string key; // events.json の giveItem/itemKey が参照する文字列キー(任意。大事なもの等で使用)
        public string itemName; // アイテム名
        public ItemCategory category; // カテゴリ
        public string description; // 説明
        public string effect; // 効果
    }

    public static class ItemCategoryExtensions
    {
        public static string ToDisplayName(this ItemCategory category) =>
            category switch
            {
                ItemCategory.Weapon => "武器",
                ItemCategory.Equipment => "装備品",
                ItemCategory.Food => "食材",
                ItemCategory.Important => "重要",
                _ => "",
            };
    }

    /// <summary>
    /// 調合でロールされた個体ステータス1つ(付与ステータスの型 + 値)。
    /// stat は Specification §1.1「アイテムカテゴリと付与ステータス」の型名(例: "attackPct")。
    /// </summary>
    [System.Serializable]
    public sealed class RolledStat
    {
        public string stat;
        public float value;

        public RolledStat() { }

        public RolledStat(string stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    public class ItemStack
    {
        public ItemData Data { get; }
        public int Count { get; set; }
        public bool IsEquipped { get; set; }

        /// <summary>
        /// 調合でロールされた個体ステータス(装備品/武器の個体差)。
        /// null/空 = 未ロールのスタック品(数量でまとめる)。値あり = 個体(マージ不可)。
        /// </summary>
        public IReadOnlyList<RolledStat> RolledStats { get; }

        /// <summary>ロール済み個体か(true ならマージ・スタックしない)。</summary>
        public bool IsInstance => RolledStats != null && RolledStats.Count > 0;

        public ItemStack(ItemData data, int count = 1)
        {
            Data = data;
            Count = count;
        }

        /// <summary>ロール済み個体を1つ作る(数量は常に1・マージしない)。</summary>
        public ItemStack(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            Data = data;
            Count = 1;
            RolledStats = rolledStats;
        }
    }
}
