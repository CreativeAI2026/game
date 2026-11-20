using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// useItem の持ち物ピッカーに並べるアイテム一覧の作り方の検証。
    /// </summary>
    public class ItemPickerEntriesTests
    {
        private readonly List<ItemData> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var data in _created)
                Object.DestroyImmediate(data);
            _created.Clear();
        }

        private ItemData Data(string name, string key)
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            data.itemName = name;
            data.key = key;
            _created.Add(data);
            return data;
        }

        [Test]
        public void Build_ExcludesEquippedAndEmpty()
        {
            var a = Data("りんご", "apple");
            var gear = Data("よろい", "gear");
            var empty = Data("空", "empty");
            var stacks = new List<ItemStack>
            {
                new(a, 2),
                new(gear, 1) { IsEquipped = true },
                new(empty, 0),
            };

            var entries = ItemPickerEntries.Build(stacks);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("apple", entries[0].ItemKey);
            Assert.AreEqual(2, entries[0].Count);
        }

        [Test]
        public void Build_MergesSameItemStacks()
        {
            var gear = Data("よろい", "gear");
            var stacks = new List<ItemStack>
            {
                new(gear, new List<RolledStat> { new("atk", 1f) }),
                new(gear, new List<RolledStat> { new("atk", 2f) }),
                new(gear, new List<RolledStat> { new("atk", 3f) }) { IsEquipped = true },
            };

            var entries = ItemPickerEntries.Build(stacks);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(2, entries[0].Count, "装備中の個体は数えない");
        }

        [Test]
        public void Build_KeepsInventoryOrder_ByFirstAppearance()
        {
            var stacks = new List<ItemStack>
            {
                new(Data("ぶどう", "grapes")),
                new(Data("あんぱん", "bread")),
                new(Data("いちご", "strawberry")),
            };

            var names = ItemPickerEntries.Build(stacks).Select(e => e.Name).ToList();

            CollectionAssert.AreEqual(
                new[] { "ぶどう", "あんぱん", "いちご" },
                names,
                "並べ替えず、インベントリと同じ順(最初に出た順)"
            );
        }

        [Test]
        public void Build_KeylessItem_HasEmptyKey()
        {
            var stacks = new List<ItemStack> { new(Data("ふしぎな石", null)) };

            var entries = ItemPickerEntries.Build(stacks);

            Assert.AreEqual(string.Empty, entries[0].ItemKey);
        }

        [Test]
        public void Build_NoStacks_ReturnsEmpty()
        {
            Assert.IsEmpty(ItemPickerEntries.Build(new List<ItemStack>()));
            Assert.IsEmpty(ItemPickerEntries.Build(null));
        }
    }
}
