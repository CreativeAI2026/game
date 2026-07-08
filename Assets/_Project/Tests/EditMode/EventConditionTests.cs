using System;
using CreativeAI.Core.EventSystem;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class EventConditionTests
    {
        private static readonly Func<string, string> NoFlags = _ => string.Empty;

        [Test]
        public void Progress_Met_WhenAtOrAboveThreshold()
        {
            var c = EventCondition.Progress(5);

            Assert.IsFalse(c.IsMet(4, NoFlags));
            Assert.IsTrue(c.IsMet(5, NoFlags)); // >= (以上)
            Assert.IsTrue(c.IsMet(6, NoFlags));
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
        public void ConditionsMet_EmptyConditions_AlwaysTrue()
        {
            var def = EventDefinition.Create("always");

            Assert.IsTrue(def.ConditionsMet(0, NoFlags));
        }
    }
}
