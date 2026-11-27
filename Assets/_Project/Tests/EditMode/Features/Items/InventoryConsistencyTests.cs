using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 所持品と装備・即時食材の枠が食い違わないことの検証(枠には所持品の束しか入らず、通知は全部変え終えてから届く)。
    /// </summary>
    public class InventoryConsistencyTests
    {
        private readonly TestResidents _residents = new();
        private readonly List<Object> _created = new();
        private InventoryManager _inv;

        [SetUp]
        public void SetUp() => _inv = _residents.Add<InventoryManager>();

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            foreach (var created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void Slots_RejectStacksNotInInventory()
        {
            var gear = new ItemStack(Make<ItemData>(ItemCategory.Equipment), 1);
            var food = new ItemStack(Make<FoodData>(ItemCategory.Food), 1);

            Assert.IsFalse(_inv.Equipment.Set(0, gear));
            Assert.IsFalse(_inv.QuickFood.Set(0, food));
        }

        [Test]
        public void UsingUpEquippedStack_ObserversSeeItGoneFromBoth()
        {
            var gear = Equip();
            var seen = WatchAll();

            _inv.ConsumeFromStack(gear, 1);

            Assert.AreEqual(new[] { "inventory", "equipment" }, seen.ToArray());
        }

        [Test]
        public void UsingUpQuickFood_ObserversSeeItGoneFromBoth()
        {
            var food = _inv.AddItem(Make<FoodData>(ItemCategory.Food), 1);
            _inv.QuickFood.Set(0, food);
            var seen = WatchAll();

            _inv.ConsumeFromStack(food, 1);

            Assert.AreEqual(new[] { "inventory", "quickFood" }, seen.ToArray());
        }

        [Test]
        public void Clear_ObserversSeeEverythingEmpty()
        {
            Equip();
            var food = _inv.AddItem(Make<FoodData>(ItemCategory.Food), 1);
            _inv.QuickFood.Set(0, food);
            var seen = WatchAll();

            _inv.Clear();

            Assert.AreEqual(new[] { "inventory", "equipment", "quickFood" }, seen.ToArray());
            Assert.IsEmpty(_inv.GetAllItems());
        }

        [Test]
        public void Restore_ObserversSeeTheWholeLoadedState()
        {
            var gear = new ItemStack(Make<ItemData>(ItemCategory.Equipment), 1);
            var food = new ItemStack(Make<FoodData>(ItemCategory.Food), 3);
            var seen = WatchAll(() =>
            {
                Assert.AreEqual(2, _inv.GetAllItems().Count, "所持品はそろっている");
                Assert.AreSame(gear, _inv.Equipment.GetSlots()[1], "装備の枠もそろっている");
                Assert.AreSame(food, _inv.QuickFood.GetSlots()[2], "食材の枠もそろっている");
            });

            _inv.Restore(
                new[] { gear, food },
                new ItemStack[] { null, gear, null },
                new ItemStack[] { null, null, food }
            );

            Assert.AreEqual(new[] { "inventory", "equipment", "quickFood" }, seen.ToArray());
        }

        [Test]
        public void Restore_DropsSlotEntriesThatDoNotFit()
        {
            var gear = new ItemStack(Make<ItemData>(ItemCategory.Equipment), 1);
            var outside = new ItemStack(Make<ItemData>(ItemCategory.Equipment), 1);

            _inv.Restore(
                new[] { gear },
                new[] { outside, gear, null },
                new ItemStack[] { gear, null, null } // 食材でない
            );

            Assert.IsNull(_inv.Equipment.GetSlots()[0], "所持品に無い束は入れない");
            Assert.AreSame(gear, _inv.Equipment.GetSlots()[1]);
            Assert.IsNull(_inv.QuickFood.GetSlots()[0], "食材でない束は食材の枠に入れない");
        }

        // 所持品と枠の通知を受けた順を記録する。どの通知の時点でも、枠の中身は所持品にある束だけであることも確かめる。
        private List<string> WatchAll(System.Action check = null)
        {
            var seen = new List<string>();
            void Record(string name)
            {
                seen.Add(name);
                var items = _inv.GetAllItems();
                foreach (var stack in _inv.Equipment.GetSlots().Concat(_inv.QuickFood.GetSlots()))
                    if (stack != null)
                        Assert.Contains(
                            stack,
                            items,
                            $"{name} の通知時点で、枠に所持品に無い束が残っている"
                        );
                check?.Invoke();
            }
            _inv.Changed += () => Record("inventory");
            _inv.Equipment.Changed += () => Record("equipment");
            _inv.QuickFood.Changed += () => Record("quickFood");
            return seen;
        }

        private ItemStack Equip()
        {
            var gear = _inv.AddInstance(
                Make<ItemData>(ItemCategory.Equipment),
                new[] { new RolledStat(nameof(StatType.AttackPct), 5f) }
            );
            _inv.Equipment.Set(0, gear);
            return gear;
        }

        private T Make<T>(ItemCategory category)
            where T : ItemData
        {
            var data = ScriptableObject.CreateInstance<T>();
            data.category = category;
            _created.Add(data);
            return data;
        }
    }
}
