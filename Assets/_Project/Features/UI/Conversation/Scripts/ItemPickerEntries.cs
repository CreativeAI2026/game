using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// useItem の持ち物ピッカーに並べるアイテム一覧を作る。未装備で所持数が1以上のものを、
    /// 同じアイテムごとに合算して名前順に並べる(装備品の個体は1つにまとめる)。
    /// </summary>
    internal static class ItemPickerEntries
    {
        /// <summary>一覧の1行。<see cref="ItemKey"/> は ItemData.key(無ければ空文字)。</summary>
        internal readonly struct Entry
        {
            public Entry(string itemKey, string name, Sprite icon, int count)
            {
                ItemKey = itemKey;
                Name = name;
                Icon = icon;
                Count = count;
            }

            public string ItemKey { get; }
            public string Name { get; }
            public Sprite Icon { get; }
            public int Count { get; }
        }

        public static List<Entry> Build(IEnumerable<ItemStack> stacks)
        {
            if (stacks == null)
                return new List<Entry>();

            return stacks
                .Where(stack => stack != null && stack.Data != null)
                .Where(stack => !stack.IsEquipped && stack.Count > 0)
                .GroupBy(stack => stack.Data)
                .Select(group => new Entry(
                    group.Key.key ?? string.Empty,
                    group.Key.itemName ?? string.Empty,
                    group.Key.icon,
                    group.Sum(stack => stack.Count)
                ))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();
        }
    }
}
