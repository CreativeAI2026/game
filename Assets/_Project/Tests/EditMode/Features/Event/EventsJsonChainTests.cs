using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 実際の events.json の進行度が、ルートごとに欠番も重複もなく 1 つずつつながっていることの検証。
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

        /// <summary>
        /// フラグでルートが分かれていても、ルートごとに進行度が 0 から 1 つずつつながっていることを確かめる。
        /// </summary>
        [Test]
        public void EventsJson_ProgressStartsFromZeroAndAdvancesByOneEachEvent()
        {
            // repeat を書いたイベント(調べる・ガード等)は進行度の連番に加わらないので除く。
            var events = EventImporter
                .Parse(File.ReadAllText(Source))
                .Events.Where(ev => ev.Repeat == EventRepeat.Progress)
                .ToList();

            foreach (var route in Routes(events))
            {
                var routeEvents = events
                    .Where(ev => RouteFlags(ev).IsSubsetOf(route))
                    .OrderBy(StartProgress)
                    .ToList();
                CollectionAssert.AreEqual(
                    Enumerable.Range(0, routeEvents.Count).ToList(),
                    routeEvents.Select(StartProgress).ToList(),
                    $"ルート [{string.Join(", ", route)}] の進行度の始まりに、欠番か重複があります(差し込んだイベントより後ろをずらし忘れていませんか)。"
                );
            }
            foreach (var ev in events)
                Assert.AreEqual(
                    StartProgress(ev) + 1,
                    ev.NextProgress,
                    $"{ev.Id} の nextProgress は、始まりの進行度の次になるはずです。"
                );
        }

        /// <summary>イベントが属するルートを表す、フラグ条件(not でないもの)の「キー=値」の集合。</summary>
        private static HashSet<string> RouteFlags(EventDefinition ev) =>
            ev
                .Conditions.Where(c => c.Type == ConditionType.Flag && !c.Negated)
                .Select(c => $"{c.FlagKey}={c.FlagValue}")
                .ToHashSet();

        /// <summary>
        /// ルートの一覧。他のどのイベントのフラグ集合にも含まれない集合を、1 本のルートの終わりとみなす。
        /// フラグ条件のないイベントは、すべてのルートの共通部分になる。
        /// </summary>
        private static List<HashSet<string>> Routes(List<EventDefinition> events)
        {
            var flagSets = events.Select(RouteFlags).ToList();
            var routes = new List<HashSet<string>>();
            foreach (var set in flagSets)
            {
                bool covered = flagSets.Any(other =>
                    other.Count > set.Count && set.IsSubsetOf(other)
                );
                if (!covered && !routes.Any(r => r.SetEquals(set)))
                    routes.Add(set);
            }
            return routes;
        }
    }
}
