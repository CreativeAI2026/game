using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// エディターのメニューから焦り度ログを集計する。
    /// テストする人から届いた PanicLogs フォルダ（いくつあってもよい）を入れたフォルダを選ぶと、
    /// 中の panic_*.csv をすべて読み、同じフォルダに報告書（.txt）と場面ごとの表（.csv）を書き出す。
    /// </summary>
    public static class PanicLogAnalyzerMenu
    {
        private const string LogFilePattern = "panic_*.csv";
        private const string MomentsSuffix = "_moments.csv";
        private const string ReviewSuffix = "_review.csv";

        [MenuItem("Tools/CreativeAI/焦り度/ログを集計…")]
        private static void AnalyzeFromMenu()
        {
            string defaultFolder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "Panic")
            );
            string folder = EditorUtility.OpenFolderPanel(
                "焦り度ログのフォルダを選ぶ",
                defaultFolder,
                string.Empty
            );
            if (string.IsNullOrEmpty(folder))
                return;

            string[] allFiles = Directory.GetFiles(
                folder,
                LogFilePattern,
                SearchOption.AllDirectories
            );
            // 同じ名前で始まる場面の一覧（_moments.csv）と振り返りの答え（_review.csv）は、ログ本体とは分けて読む
            string[] files = allFiles
                .Where(f => !f.EndsWith(MomentsSuffix) && !f.EndsWith(ReviewSuffix))
                .OrderBy(f => f)
                .ToArray();
            string[] reviewFiles = allFiles
                .Where(f => f.EndsWith(ReviewSuffix))
                .OrderBy(f => f)
                .ToArray();
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "焦り度ログの集計",
                    $"{folder} に panic_*.csv がありません。",
                    "OK"
                );
                return;
            }

            PanicLogAnalysis analysis = PanicLogAnalyzer.Analyze(files.Select(PanicLogCsv.Load));
            PanicLogAnalyzer.AddReviewAnswers(analysis, reviewFiles.Select(PanicLogCsv.Load));

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportPath = Path.Combine(folder, $"集計_{stamp}.txt");
            string tablePath = Path.Combine(folder, $"集計_{stamp}_場面.csv");
            // Excel で文字化けしないよう BOM 付き UTF-8 で書く
            var encoding = new UTF8Encoding(true);
            File.WriteAllText(reportPath, PanicLogReportWriter.BuildReport(analysis), encoding);
            File.WriteAllText(tablePath, PanicLogReportWriter.BuildSceneTable(analysis), encoding);

            Debug.Log($"[焦り度ログ] {files.Length} 件を集計しました: {reportPath}");
            EditorUtility.OpenWithDefaultApp(reportPath);
        }
    }
}
