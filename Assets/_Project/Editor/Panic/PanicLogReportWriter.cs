using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 焦り度ログの集計結果（<see cref="PanicLogAnalysis"/>）を、開発者が読む報告書と、場面ごとの表に書き起こす。
    /// 報告書の時刻（秒）は開発者がログを開いて確かめるためのもので、テストする人に見せるものではない。
    /// </summary>
    public static class PanicLogReportWriter
    {
        // 取りこぼし・誤検知候補の一覧に、指標の最大スコアをいくつまで並べるか
        private const int TopSignalsToShow = 3;

        public static string BuildReport(PanicLogAnalysis analysis)
        {
            var text = new StringBuilder();
            text.AppendLine("焦り度ログの集計");
            text.AppendLine("================");
            text.AppendLine($"ログの数: {analysis.FileCount}");
            text.AppendLine($"記録時間: {analysis.RecordedSeconds:F0} 秒（うち交戦中 {analysis.CombatSeconds:F0} 秒）");
            text.AppendLine($"場面の数: {analysis.Scenes.Count}（交戦の開始・ボスの攻撃の出だし・被弾・掴まれた で区切ったもの）");
            text.AppendLine($"「焦った」の印: {analysis.Markers.Count}");
            text.AppendLine($"「焦り」の判定: {analysis.Detections.Count} 回");
            text.AppendLine();

            AppendMatchRates(text, analysis);
            AppendReviewAnswers(text, analysis);
            AppendSignalComparison(text, analysis);
            AppendMissedMarkers(text, analysis);
            AppendUnmarkedDetections(text, analysis);
            AppendPerControlScheme(text, analysis);
            return text.ToString();
        }

        /// <summary>場面ごとの表（1行1場面）。指標ごとの最大スコアも並べ、表計算ソフトで絞り込めるようにする。</summary>
        public static string BuildSceneTable(PanicLogAnalysis analysis)
        {
            var table = new StringBuilder();
            table.Append("file,scene,trigger,start,end,has_marker,has_panic,has_tense_or_above,max_panic_score,category_at_max,control_scheme");
            foreach (string column in analysis.SignalColumns)
            {
                table.Append(',').Append(column);
            }
            table.AppendLine();

            foreach (PanicLogScene scene in analysis.Scenes)
            {
                table.Append(scene.FileName);
                table.Append(',').Append(scene.Index);
                table.Append(',').Append(scene.Trigger);
                table.Append(',').Append(Format(scene.StartTime));
                table.Append(',').Append(Format(scene.EndTime));
                table.Append(',').Append(scene.HasMarker ? 1 : 0);
                table.Append(',').Append(scene.HasPanic ? 1 : 0);
                table.Append(',').Append(scene.HasTenseOrAbove ? 1 : 0);
                table.Append(',').Append(Format(scene.MaxPanicScore));
                table.Append(',').Append(scene.CategoryAtMax);
                table.Append(',').Append(scene.ControlScheme);
                foreach (string column in analysis.SignalColumns)
                {
                    scene.MaxSignalScores.TryGetValue(column, out float value);
                    table.Append(',').Append(Format(value));
                }
                table.AppendLine();
            }
            return table.ToString();
        }

        private static void AppendMatchRates(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 印と判定の一致（印はその場面かひとつ前の場面、判定はその場面か次の場面で照合）");
            int markers = analysis.Markers.Count;
            int matchedPanic = analysis.Markers.Count(m => m.MatchedPanic);
            int matchedTense = analysis.Markers.Count(m => m.MatchedTenseOrAbove);
            text.AppendLine($"  印のうち「焦り」と判定できていた: {Rate(matchedPanic, markers)}");
            text.AppendLine($"  印のうち「動揺」以上と判定できていた: {Rate(matchedTense, markers)}");

            int detections = analysis.Detections.Count;
            int marked = analysis.Detections.Count(d => d.HasMarker);
            text.AppendLine($"  「焦り」の判定のうち印が付いていた: {Rate(marked, detections)}");
            text.AppendLine("  ※ 押し忘れがあると、最後の割合は実際より低く出る（印の無い判定がすべて誤検知とは限らない）");
            text.AppendLine();
        }

        // 振り返りの答えで、判定が当たっていた割合を見る（印の押し忘れに左右されない、誤検知の目安）
        private static void AppendReviewAnswers(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 終了時の振り返り（「焦り」と判定した場面に、本人が答えたもの）");
            List<PanicLogReviewAnswer> answers = analysis.ReviewAnswers;
            if (answers.Count == 0)
            {
                text.AppendLine("  答えなし");
                text.AppendLine();
                return;
            }

            AppendAnswerLine(text, "全体", answers);
            foreach (IGrouping<string, PanicLogReviewAnswer> group in answers.GroupBy(a => a.Category))
            {
                AppendAnswerLine(text, $"系統「{group.Key}」", group.ToList());
            }
            foreach (IGrouping<string, PanicLogReviewAnswer> group in answers.GroupBy(a => a.MainSignal))
            {
                AppendAnswerLine(text, $"中心の指標「{group.Key}」", group.ToList());
            }
            text.AppendLine("  ※ 当たっていた割合は「焦っていた」÷（「焦っていた」＋「焦っていなかった」）。覚えていない・未回答は除く");
            text.AppendLine();
        }

        private static void AppendAnswerLine(StringBuilder text, string label, List<PanicLogReviewAnswer> answers)
        {
            int yes = answers.Count(a => a.Answer == "焦っていた");
            int no = answers.Count(a => a.Answer == "焦っていなかった");
            int unknown = answers.Count - yes - no;
            text.AppendLine(
                $"  {label}: 焦っていた {yes} / 焦っていなかった {no} / 覚えていない・未回答 {unknown} → 当たっていた割合 {Rate(yes, yes + no)}"
            );
        }

        // 指標ごとに、印のある場面とない場面での最大スコアの平均を比べる。差が大きいほど焦りを見分けられている
        private static void AppendSignalComparison(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 指標ごとの見分けの目安（場面ごとの最大スコアの平均）");
            List<PanicLogScene> marked = analysis.Scenes.Where(s => s.HasMarker).ToList();
            List<PanicLogScene> unmarked = analysis.Scenes.Where(s => !s.HasMarker).ToList();
            text.AppendLine($"  （印のある場面 {marked.Count} / 印のない場面 {unmarked.Count}）");
            foreach (string column in analysis.SignalColumns)
            {
                float markedAverage = AverageMax(marked, column);
                float unmarkedAverage = AverageMax(unmarked, column);
                text.AppendLine(
                    $"  {column.Replace("_スコア", string.Empty),-6}: 印あり {markedAverage:F2} / 印なし {unmarkedAverage:F2}（差 {markedAverage - unmarkedAverage:+0.00;-0.00}）"
                );
            }
            text.AppendLine();
        }

        private static void AppendMissedMarkers(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 取りこぼし（印があるのに「焦り」と判定していない場面）");
            List<PanicLogMarker> missed = analysis.Markers.Where(m => !m.MatchedPanic).ToList();
            if (missed.Count == 0)
            {
                text.AppendLine("  なし");
            }
            foreach (PanicLogMarker marker in missed)
            {
                PanicLogScene scene = marker.Scene;
                text.AppendLine(
                    $"  {marker.FileName} {Format(marker.Time)}秒 / きっかけ: {scene.Trigger} / 最大焦り度 {scene.MaxPanicScore:F0} / 高かった指標: {TopSignals(scene)}"
                );
            }
            text.AppendLine();
        }

        private static void AppendUnmarkedDetections(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 誤検知の候補（「焦り」と判定したのに印が無い）");
            List<PanicLogDetection> unmarked = analysis.Detections.Where(d => !d.HasMarker).ToList();
            if (unmarked.Count == 0)
            {
                text.AppendLine("  なし");
            }

            foreach (IGrouping<string, PanicLogDetection> group in unmarked.GroupBy(d => d.CategoryAtMax))
            {
                text.AppendLine($"  系統「{group.Key}」: {group.Count()} 回");
            }
            foreach (PanicLogDetection detection in unmarked)
            {
                text.AppendLine(
                    $"  {detection.FileName} {Format(detection.StartTime)}〜{Format(detection.EndTime)}秒 / 系統 {detection.CategoryAtMax} / きっかけ: {detection.StartScene.Trigger} / 最大焦り度 {detection.MaxPanicScore:F0}"
                );
            }
            text.AppendLine();
        }

        private static void AppendPerControlScheme(StringBuilder text, PanicLogAnalysis analysis)
        {
            text.AppendLine("■ 操作機器ごと（場面の数 / 印 / 「焦り」の判定があった場面）");
            foreach (IGrouping<string, PanicLogScene> group in analysis.Scenes.GroupBy(s => string.IsNullOrEmpty(s.ControlScheme) ? "記録なし" : s.ControlScheme))
            {
                text.AppendLine(
                    $"  {group.Key}: {group.Count()} / {group.Count(s => s.HasMarker)} / {group.Count(s => s.HasPanic)}"
                );
            }
        }

        private static string TopSignals(PanicLogScene scene)
        {
            IEnumerable<string> top = scene
                .MaxSignalScores.OrderByDescending(pair => pair.Value)
                .Take(TopSignalsToShow)
                .Select(pair => $"{pair.Key.Replace("_スコア", string.Empty)} {pair.Value:F2}");
            return string.Join("、", top);
        }

        private static float AverageMax(List<PanicLogScene> scenes, string column)
        {
            if (scenes.Count == 0)
                return 0f;
            float sum = 0f;
            foreach (PanicLogScene scene in scenes)
            {
                scene.MaxSignalScores.TryGetValue(column, out float value);
                sum += value;
            }
            return sum / scenes.Count;
        }

        private static string Rate(int count, int total) =>
            total == 0 ? "（対象なし）" : $"{count} / {total}（{100f * count / total:F0}%）";

        private static string Format(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
