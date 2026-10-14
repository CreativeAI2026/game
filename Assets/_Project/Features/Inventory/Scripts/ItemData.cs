using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public enum ItemCategory
    {
        // 明示値で既存アセットの serialized category(1/2/3)を保つ。
        // 武器はインベントリ在庫の対象外(WeaponManager 管理)なので、この3カテゴリに含めない。
        Equipment = 1,
        Food = 2,
        Important = 3,
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
        public ItemData material1; // 合成後アイテムの素材(合成前は null)
        public ItemData material2;

        [SerializeField, Min(1)]
        private int _maxStack = 1;

        /// <summary>1スタックに積める上限。既定は1(装備品・大事なもの=積まない)。カテゴリで積めるものは派生でルール化する。</summary>
        public virtual int MaxStack => Mathf.Max(1, _maxStack);
    }

    /// <summary>
    /// 所持品の1枠(スタック)。同じアイテムを個数ぶん積み重ねたもの。
    /// 抽選された能力値(RolledStats)を持つ装備品は個体なので、1個ずつ別の枠になる。
    /// </summary>
    public class ItemStack
    {
        public ItemData Data { get; }
        private int _count;

        public int Count
        {
            get => IsInstance ? 1 : _count;
            set => _count = IsInstance ? 1 : value;
        }

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

        /// <summary>ロール済み個体を1つ作る（数量は常に1・マージしない）。</summary>
        public ItemStack(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            Data = data;
            RolledStats = rolledStats;
            Count = 1;
        }
    }

    public static class ItemCategoryExtensions
    {
        public static string ToDisplayName(this ItemCategory category) =>
            category switch
            {
                ItemCategory.Equipment => "装備品",
                ItemCategory.Food => "食材",
                ItemCategory.Important => "大事なもの",
                _ => "",
            };
    }
}
