using System;
using CreativeAI.Core;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベント発生条件(進行度・フラグ・所持アイテムの AND 判定)の検証。
    /// </summary>
    public class EventConditionTests
    {
        private static readonly Func<string, string> NoFlags = _ => string.Empty;

        [Test]
        public void Progress_Met_OnlyWhenExactlyEqual()
        {
            var c = EventCondition.Progress(5);

            Assert.IsFalse(c.IsMet(4, NoFlags));
            Assert.IsTrue(c.IsMet(5, NoFlags)); // == (ちょうど一致)
            Assert.IsFalse(c.IsMet(6, NoFlags)); // 進行度が進むと二度と発火しない
        }

        [Test]
        public void Flag_Met_OnlyWhenValueMatches()
        {
            var c = EventCondition.Flag("girl_choice", "together");
            Func<string, string> flags = key => key == "girl_choice" ? "together" : string.Empty;

            Assert.IsTrue(c.IsMet(0, flags));
            Assert.IsFalse(c.IsMet(0, key => "alone"));
            Assert.IsFalse(c.IsMet(0, NoFlags)); // 未設定
        }

        [Test]
        public void ConditionsMet_RequiresAllToPass_AND()
        {
            var def = EventDefinition.Create(
                "girl_reunion",
                EventCondition.Progress(8),
                EventCondition.Flag("girl_choice", "together")
            );
            Func<string, string> together = _ => "together";

            Assert.IsTrue(def.ConditionsMet(8, together));
            Assert.IsFalse(def.ConditionsMet(7, together)); // progress 不足
            Assert.IsFalse(def.ConditionsMet(8, _ => "alone")); // flag 不一致
        }

        [Test]
        public void HasItem_Met_OnlyWhenOwned()
        {
            var c = EventCondition.HasItem("mysterious_key");
            Func<string, bool> owns = key => key == "mysterious_key";

            Assert.IsTrue(c.IsMet(0, NoFlags, owns));
            Assert.IsFalse(c.IsMet(0, NoFlags, _ => false)); // 未所持
            Assert.IsFalse(c.IsMet(0, NoFlags)); // hasItem 未指定なら不成立
        }

        [Test]
        public void ConditionsMet_HasItem_AndedWithProgress()
        {
            var def = EventDefinition.Create(
                "locked_door",
                EventCondition.Progress(10),
                EventCondition.HasItem("mysterious_key")
            );
            Func<string, bool> hasKey = key => key == "mysterious_key";

            Assert.IsTrue(def.ConditionsMet(10, NoFlags, hasKey));
            Assert.IsFalse(def.ConditionsMet(10, NoFlags, _ => false)); // 鍵なし
            Assert.IsFalse(def.ConditionsMet(9, NoFlags, hasKey)); // progress 不足
            Assert.IsFalse(def.ConditionsMet(10, NoFlags)); // hasItem 未供給
        }

        [Test]
        public void Not_InvertsEachConditionType()
        {
            Func<string, string> crafted = key => key == "crafted" ? "1" : string.Empty;
            Func<string, bool> owns = key => key == "card_key";

            var notFlag = EventCondition.Flag("crafted", "1", not: true);
            Assert.IsFalse(notFlag.IsMet(0, crafted)); // 立っている → 満たさない
            Assert.IsTrue(notFlag.IsMet(0, NoFlags)); // 未設定 → 満たす

            var notProgress = EventCondition.Progress(5, not: true);
            Assert.IsFalse(notProgress.IsMet(5, NoFlags));
            Assert.IsTrue(notProgress.IsMet(6, NoFlags));

            var notItem = EventCondition.HasItem("card_key", not: true);
            Assert.IsFalse(notItem.IsMet(0, NoFlags, owns));
            Assert.IsTrue(notItem.IsMet(0, NoFlags, _ => false));
        }

        [Test]
        public void ConditionsMet_GuardPattern_ProgressAndNotCrafted()
        {
            // 出口のガード: 進行度 5 の間、合成するまで成立する。
            var def = EventDefinition.Create(
                "craft_room_exit",
                EventCondition.Progress(5),
                EventCondition.Flag(ProgressFlags.Crafted, "1", not: true)
            );

            Assert.IsTrue(def.ConditionsMet(5, NoFlags));
            Assert.IsFalse(def.ConditionsMet(5, _ => "1")); // 合成済み
            Assert.IsFalse(def.ConditionsMet(6, NoFlags)); // 章が違う
        }

        [Test]
        public void ConditionsMet_Once_StopsAfterSeenFlag()
        {
            var def = EventDefinition.Create(
                "drawer_once",
                new[] { EventCondition.Progress(6) },
                System.Array.Empty<EventStep>(),
                0,
                EventRepeat.Once,
                advancesProgress: false
            );
            Func<string, string> seen = key =>
                key == ProgressFlags.Seen("drawer_once") ? ProgressFlags.SeenValue : string.Empty;

            Assert.IsTrue(def.ConditionsMet(6, NoFlags));
            Assert.IsFalse(def.ConditionsMet(6, seen)); // 見終えたら二度と発火しない
        }

        [Test]
        public void ConditionsMet_Always_IgnoresSeenFlag()
        {
            var def = EventDefinition.Create(
                "shelf_check",
                new[] { EventCondition.Progress(6) },
                System.Array.Empty<EventStep>(),
                0,
                EventRepeat.Always,
                advancesProgress: false
            );

            Assert.IsTrue(def.ConditionsMet(6, _ => ProgressFlags.SeenValue));
        }

        [Test]
        public void ConditionsMet_EmptyConditions_IsInvalidData_FallsBackToTrue()
        {
            // conditions は必須で progress を必ず1つ含むため、
            // 条件0件のイベントは存在しない。Importer が取り込み時に弾く
            // (EventImporterTests.Parse_MissingProgressCondition_IsError)。
            // ここで固定するのは「万一そうなっても例外にせず真を返す」フォールバック挙動であって、
            // 「条件なしイベントが作れる」という仕様ではない。
            var def = EventDefinition.Create("invalid_no_conditions");

            Assert.IsTrue(def.ConditionsMet(0, NoFlags));
        }
    }
}
