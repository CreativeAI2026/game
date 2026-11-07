using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.EditorTools;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>イベント定義から「発火直前の開発用状態」を組み立てる処理の検証。</summary>
    public class DevStateBuilderTests
    {
        private readonly List<EventDefinition> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var ev in _created)
                Object.DestroyImmediate(ev);
            _created.Clear();
        }

        private EventDefinition Make(
            string id,
            EventCondition[] conditions,
            params EventStep[] steps
        )
        {
            var ev = EventDefinition.Create(id, conditions, steps, 99);
            _created.Add(ev);
            return ev;
        }

        [Test]
        public void StartProgress_ReadsTheProgressCondition_AndIsMinusOneWithoutOne()
        {
            var withProgress = Make(
                "a",
                new[] { EventCondition.Flag("k", "v"), EventCondition.Progress(7) }
            );
            var without = Make("b", new[] { EventCondition.Flag("k", "v") });

            Assert.AreEqual(7, DevStateBuilder.StartProgress(withProgress));
            Assert.AreEqual(-1, DevStateBuilder.StartProgress(without));
            Assert.AreEqual(-1, DevStateBuilder.StartProgress(null));
        }

        [Test]
        public void SortByProgress_OrdersByProgressThenId()
        {
            var late = Make("late", new[] { EventCondition.Progress(5) });
            var earlyB = Make("b", new[] { EventCondition.Progress(1) });
            var earlyA = Make("a", new[] { EventCondition.Progress(1) });

            var sorted = DevStateBuilder.SortByProgress(new[] { late, earlyB, earlyA });

            CollectionAssert.AreEqual(new[] { earlyA, earlyB, late }, sorted);
        }

        [Test]
        public void ForEvent_CopiesProgressFlagsAndRequiredItemsFromTheConditions()
        {
            var target = Make(
                "target",
                new[]
                {
                    EventCondition.Progress(8),
                    EventCondition.Flag("girl_choice", "together"),
                    EventCondition.HasItem("card_key"),
                }
            );

            var state = DevStateBuilder.ForEvent(target, new[] { target }, false);

            Assert.AreEqual(8, state.progress);
            Assert.AreEqual(1, state.flags.Length);
            Assert.AreEqual("girl_choice", state.flags[0].key);
            Assert.AreEqual("together", state.flags[0].value);
            CollectionAssert.AreEqual(new[] { "card_key" }, state.importantItems);
            Assert.IsTrue(state.HasHp);
        }

        [Test]
        public void ForEvent_WithEarlierRewards_GivesOnlyWhatEarlierEventsHand_OutWithoutDuplicates()
        {
            var first = Make(
                "first",
                new[] { EventCondition.Progress(1) },
                EventStep.GiveWeapon("scythe"),
                EventStep.GiveItem("apple")
            );
            var second = Make(
                "second",
                new[] { EventCondition.Progress(2) },
                EventStep.GiveItem("apple"),
                EventStep.GiveItem("note")
            );
            var target = Make(
                "target",
                new[] { EventCondition.Progress(3) },
                EventStep.GiveWeapon("bow")
            );
            var later = Make(
                "later",
                new[] { EventCondition.Progress(4) },
                EventStep.GiveItem("hat")
            );

            var state = DevStateBuilder.ForEvent(
                target,
                new[] { later, target, second, first },
                true
            );

            CollectionAssert.AreEqual(new[] { "scythe" }, state.weapons);
            CollectionAssert.AreEqual(new[] { "apple", "note" }, state.importantItems);
        }

        [Test]
        public void ForEvent_WithoutEarlierRewards_LeavesItemsAndWeaponsEmpty()
        {
            var first = Make(
                "first",
                new[] { EventCondition.Progress(1) },
                EventStep.GiveWeapon("scythe")
            );
            var target = Make("target", new[] { EventCondition.Progress(2) });

            var state = DevStateBuilder.ForEvent(target, new[] { first, target }, false);

            Assert.IsEmpty(state.weapons);
            Assert.IsEmpty(state.importantItems);
        }

        [Test]
        public void ForEvent_KeepsTheSpawnSettingFromBasis()
        {
            var target = Make("target", new[] { EventCondition.Progress(2) });
            var basis = new DevState
            {
                useSpawn = true,
                spawnPosition = new Vector3(-592f, -60.8f, -30.5f),
                spawnYaw = 45f,
            };

            var state = DevStateBuilder.ForEvent(target, new[] { target }, false, basis);
            var fresh = DevStateBuilder.ForEvent(target, new[] { target }, false);

            Assert.IsTrue(state.useSpawn);
            Assert.AreEqual(basis.spawnPosition, state.spawnPosition);
            Assert.AreEqual(45f, state.spawnYaw);
            Assert.IsFalse(fresh.useSpawn);
        }

        [Test]
        public void ForEvent_KeepsHpAndSelectedWeaponFromBasis_OnlyWhileTheWeaponIsStillOwned()
        {
            var first = Make(
                "first",
                new[] { EventCondition.Progress(1) },
                EventStep.GiveWeapon("scythe")
            );
            var target = Make("target", new[] { EventCondition.Progress(2) });
            var basis = new DevState { hp = 250f, selectedWeapon = "scythe" };

            var kept = DevStateBuilder.ForEvent(target, new[] { first, target }, true, basis);
            basis.selectedWeapon = "bow";
            var dropped = DevStateBuilder.ForEvent(target, new[] { first, target }, true, basis);

            Assert.AreEqual(250f, kept.hp);
            Assert.AreEqual("scythe", kept.selectedWeapon);
            Assert.AreEqual(string.Empty, dropped.selectedWeapon);
        }
    }
}
