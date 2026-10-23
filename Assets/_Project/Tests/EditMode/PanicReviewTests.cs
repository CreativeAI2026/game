using System.Collections.Generic;
using System.Linq;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 終了時の振り返りの検証: 見せる場面の選び方（数を絞り、系統が偏らず、起きた順）と、答えの集計。
    /// </summary>
    public class PanicReviewTests
    {
        private static PanicMoment Moment(int index, PanicCategory category, float score) =>
            new PanicMoment
            {
                Index = index,
                Category = category,
                PanicScore = score,
                MainSignal = "逃避",
                Description = PanicMomentWording.Describe("逃避"),
            };

        [Test]
        public void 見せる場面は上限までに絞られる()
        {
            var moments = Enumerable.Range(1, 12).Select(i => Moment(i, PanicCategory.Avoidance, i)).ToList();

            List<PanicMoment> selected = PanicReviewSelection.Select(moments, 8);

            Assert.That(selected.Count, Is.EqualTo(8));
        }

        [Test]
        public void 焦り度が高い系統に偏らず系統ごとに選ばれる()
        {
            var moments = new List<PanicMoment>
            {
                Moment(1, PanicCategory.Avoidance, 90),
                Moment(2, PanicCategory.Avoidance, 85),
                Moment(3, PanicCategory.Avoidance, 80),
                Moment(4, PanicCategory.Freeze, 62),
                Moment(5, PanicCategory.Indecision, 61),
            };

            List<PanicMoment> selected = PanicReviewSelection.Select(moments, 3);

            CollectionAssert.AreEquivalent(
                new[] { PanicCategory.Avoidance, PanicCategory.Freeze, PanicCategory.Indecision },
                selected.Select(m => m.Category).ToArray()
            );
        }

        [Test]
        public void 見せる順は起きた順になる()
        {
            var moments = new List<PanicMoment>
            {
                Moment(1, PanicCategory.Freeze, 61),
                Moment(2, PanicCategory.Avoidance, 95),
                Moment(3, PanicCategory.Indecision, 70),
            };

            List<PanicMoment> selected = PanicReviewSelection.Select(moments, 8);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, selected.Select(m => m.Index).ToArray());
        }

        [Test]
        public void 場面の説明に直前の出来事が添えられる()
        {
            PanicMoment moment = Moment(1, PanicCategory.Avoidance, 70);
            moment.RecentEvent = "ボスの鎌の攻撃の直後";

            Assert.That(moment.ReviewText, Is.EqualTo("背を向けてボスから離れていたとき（ボスの鎌の攻撃の直後）"));
        }

        [Test]
        public void 振り返りの答えが当たっていた割合として集計される()
        {
            var lines = new List<string>
            {
                "moment,time,category,main_signal,description,recent_event,panic_score,image,answer",
                "1,10,逃げ腰,逃避,背を向けて,,70,,焦っていた",
                "2,30,逃げ腰,逃避,背を向けて,,70,,焦っていなかった",
                "3,50,硬直,硬直,動けなかった,,66,,焦っていた",
                "4,70,硬直,硬直,動けなかった,,66,,覚えていない",
            };
            var analysis = new PanicLogAnalysis();

            PanicLogAnalyzer.AddReviewAnswers(analysis, new[] { PanicLogCsv.Parse("test_review.csv", lines) });
            string report = PanicLogReportWriter.BuildReport(analysis);

            Assert.That(analysis.ReviewAnswers.Count, Is.EqualTo(4));
            StringAssert.Contains("全体: 焦っていた 2 / 焦っていなかった 1 / 覚えていない・未回答 1 → 当たっていた割合 2 / 3（67%）", report);
            StringAssert.Contains("系統「硬直」: 焦っていた 1 / 焦っていなかった 0 / 覚えていない・未回答 1 → 当たっていた割合 1 / 1（100%）", report);
        }
    }
}
