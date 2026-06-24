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

    public class ItemStack
    {
        public ItemData Data { get; }
        public int Count { get; set; }
        public bool IsEquipped { get; set; }

        public ItemStack(ItemData data, int count = 1)
        {
            Data = data;
            Count = count;
        }
    }
}
