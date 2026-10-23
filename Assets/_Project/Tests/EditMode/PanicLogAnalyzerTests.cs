using System.Collections.Generic;
using System.Linq;
using CreativeAI.EditorTools;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 焦り度ログの集計（場面の区切りと、印・判定の照合）の検証。
    /// 印は秒数ではなく「その場面か、ひとつ前の場面」で照合されることを確かめる。
    /// </summary>
    public class PanicLogAnalyzerTests
    {
        private const string Header =
            "time,row_type,detail,in_combat,enemy_attacking,player_hp_ratio,panic_score,level,dominant_category,逃避_スコア,boss_state,control_scheme";

        /// <summary>ログの行を組み立てる。</summary>
        private sealed class LogBuilder
        {
            private readonly List<string> _lines = new List<string> { Header };
            private float _time;

            public LogBuilder Sample(
                bool inCombat = true,
                bool attacking = false,
                float hp = 1f,
                string level = "平常",
                float flight = 0f,
                string bossState = "TBossWatchState"
            )
            {
                _time += 0.1f;
                string score =
                    level == "焦り" ? "70"
                    : level == "動揺" ? "35"
                    : "0";
                _lines.Add(
                    $"{_time:0.0},sample,,{(inCombat ? 1 : 0)},{(attacking ? 1 : 0)},{hp},{score},{level},逃げ腰,{flight},{bossState},KeyboardMouse"
                );
                return this;
            }

            public LogBuilder Marker(bool inCombat = true, float hp = 1f)
            {
                _time += 0.1f;
                _lines.Add(
                    $"{_time:0.0},marker,1,{(inCombat ? 1 : 0)},0,{hp},0,平常,逃げ腰,0,TBossWatchState,KeyboardMouse"
                );
                return this;
            }

            public PanicLogAnalysis Analyze() =>
                PanicLogAnalyzer.Analyze(new[] { PanicLogCsv.Parse("test.csv", _lines) });
        }

        [Test]
        public void 出来事で場面に区切られる()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample()
                .Sample(attacking: true, bossState: "TBossNormalAttack2State")
                .Sample(attacking: true, hp: 0.8f, bossState: "TBossNormalAttack2State")
                .Sample(bossState: "TBossCapturedState")
                .Analyze();

            CollectionAssert.AreEqual(
                new[] { "交戦開始", "ボスの攻撃（鎌）", "被弾", "掴まれた" },
                analysis.Scenes.Select(s => s.Trigger).ToArray()
            );
        }

        [Test]
        public void 押すのがひとつ後の場面にずれた印も一致とみなす()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(attacking: true, level: "焦り", flight: 1f)
                .Sample(attacking: true, hp: 0.8f) // 被弾で次の場面へ
                .Marker(hp: 0.8f)
                .Analyze();

            Assert.That(analysis.Markers.Single().MatchedPanic, Is.True);
        }

        [Test]
        public void 二つ以上後の場面の印は取りこぼしとみなす()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(level: "焦り", flight: 1f)
                .Sample(attacking: true) // ボスの攻撃で次の場面
                .Sample(attacking: true, hp: 0.8f) // 被弾でさらに次の場面
                .Marker(hp: 0.8f)
                .Analyze();

            Assert.That(analysis.Markers.Single().MatchedPanic, Is.False);
        }

        [Test]
        public void 判定の次の場面に付いた印も判定と一致とみなす()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(level: "焦り", flight: 1f)
                .Sample(attacking: true)
                .Marker()
                .Analyze();

            Assert.That(analysis.Detections.Single().HasMarker, Is.True);
        }

        [Test]
        public void 印の無い判定は誤検知の候補になる()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(level: "焦り", flight: 1f)
                .Sample()
                .Sample(attacking: true)
                .Sample(attacking: true, hp: 0.8f)
                .Analyze();

            Assert.That(analysis.Detections.Single().HasMarker, Is.False);
        }

        [Test]
        public void 交戦の外で付けた印は直前の場面のものとして扱う()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(attacking: true, hp: 0.2f, level: "焦り", flight: 1f)
                .Sample(inCombat: false, hp: 0f) // 倒れて交戦が終わった
                .Marker(inCombat: false, hp: 0f)
                .Analyze();

            PanicLogMarker marker = analysis.Markers.Single();
            Assert.That(marker.Scene, Is.SameAs(analysis.Scenes.Last()));
            Assert.That(marker.MatchedPanic, Is.True);
        }

        [Test]
        public void 場面ごとに指標の最大スコアを残す()
        {
            PanicLogAnalysis analysis = new LogBuilder()
                .Sample(flight: 0.3f)
                .Sample(flight: 0.9f)
                .Sample(flight: 0.5f)
                .Analyze();

            Assert.That(
                analysis.Scenes.Single().MaxSignalScores["逃避_スコア"],
                Is.EqualTo(0.9f).Within(0.0001f)
            );
        }
    }
}
