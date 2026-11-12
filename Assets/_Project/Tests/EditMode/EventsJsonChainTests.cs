using System.IO;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.EditorTools;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 実際の events.json の進行度が、欠番も重複もなく 1 つずつつながっていることの検証。
    /// イベントを途中に差し込んで進行度をずらすときの、ずらし忘れを見つけるためのもの。
    /// </summary>
    public class EventsJsonChainTests
    {
        private const string Source = "Assets/_Project/Features/Event/Data/events.json";

        private static int StartProgress(EventDefinition ev) =>
            ev.Conditions.First(c => c.Type == ConditionType.Progress).ProgressValue;

        [Test]
        public void EventsJson_ParsesWithoutErrors()
        {
            var report = EventImporter.Parse(File.ReadAllText(Source));

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
        }

        [Test]
        public void EventsJson_ProgressStartsFromZeroAndAdvancesByOneEachEvent()
        {
            // repeat を書いたイベント(調べる・ガード等)は進行度の連番に加わらないので除く。
            var events = EventImporter
                .Parse(File.ReadAllText(Source))
                .Events.Where(ev => ev.Repeat == EventRepeat.Progress)
                .OrderBy(StartProgress)
                .ToList();

            var starts = events.Select(StartProgress).ToList();
            CollectionAssert.AreEqual(
                Enumerable.Range(0, events.Count).ToList(),
                starts,
                "進行度の始まりに、欠番か重複があります(差し込んだイベントより後ろをずらし忘れていませんか)。"
            );
            foreach (var ev in events)
                Assert.AreEqual(
                    StartProgress(ev) + 1,
                    ev.NextProgress,
                    $"{ev.Id} の nextProgress は、始まりの進行度の次になるはずです。"
                );
        }
    }
}
