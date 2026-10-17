using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// Project ウィンドウで選んだ音素材の、前後の無音をまとめて削る。
    /// 無音の基準は音声エディタの「無音の自動検出」と同じ設定を使う。音程・速度など前回保存した他の編集内容はそのまま残す。
    /// </summary>
    public static class AudioSilenceTrimMenu
    {
        private const string Title = "前後の無音を削る";

        [MenuItem("Assets/SoundKit/選択した音素材の前後の無音を削る")]
        private static void TrimSelected()
        {
            List<string> paths = CollectSelectedPaths();
            if (!AudioClipEditing.IsFfmpegAvailable)
            {
                EditorUtility.DisplayDialog(Title,
                    "ffmpeg（ffmpeg.exe と ffprobe.exe）が見つかりません。インストール済みの場合は、Unity を開き直してください。", "OK");
                return;
            }

            float thresholdDb = AudioSilenceDetector.ThresholdDb;
            float marginSeconds = AudioSilenceDetector.MarginSeconds;
            if (!EditorUtility.DisplayDialog(Title,
                    $"音素材 {paths.Count} 個の前後の無音を削り、上書きします。\n" +
                    $"基準：{thresholdDb} dB 以下を無音とみなし、前後に {marginSeconds} 秒の余白を残す（音声エディタで変更できます）。\n\n" +
                    $"上書きする前のファイルはプロジェクト直下の {AudioEditStore.BackupFolder} に保存し、音声エディタの「元に戻す」で戻せます。",
                    "実行", "やめる"))
            {
                return;
            }

            var report = new StringBuilder($"[{Title}] 基準 {thresholdDb} dB・余白 {marginSeconds} 秒\n");
            int changedCount = 0, failedCount = 0;
            bool anyNormalized = false;
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    if (EditorUtility.DisplayCancelableProgressBar(Title, $"{i + 1}/{paths.Count}  {Path.GetFileName(path)}",
                            (float)i / paths.Count))
                    {
                        break;
                    }

                    string result = TrimClip(path, thresholdDb, marginSeconds, out bool changed, out bool failed);
                    report.AppendLine($"{result}  {path}");
                    if (changed)
                    {
                        changedCount++;
                        anyNormalized |= AudioLoudnessNormalizer.WasNormalized(path);
                    }
                    if (failed) failedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog(Title,
                $"削った：{changedCount} 個／失敗：{failedCount} 個\n素材ごとの結果は Console に出しました。" +
                (anyNormalized ? "\n\n音量をそろえ済みの素材を編集したため、音量をそろえ直してください。" : string.Empty), "OK");
        }

        [MenuItem("Assets/SoundKit/選択した音素材の前後の無音を削る", true)]
        private static bool CanTrimSelected() => CollectSelectedPaths().Count > 0;

        private static string TrimClip(string assetPath, float thresholdDb, float marginSeconds, out bool changed, out bool failed)
        {
            changed = false;
            failed = true;
            if (!AudioClipEditing.TryLoad(assetPath, out AudioClipEditing.Source source, out string error))
                return "失敗　" + error.Split('\n')[0];

            if (!AudioSilenceDetector.TryFindSoundRange(source.Samples, source.Channels, source.SampleRate, thresholdDb, marginSeconds,
                    out double start, out double end))
            {
                return "失敗　全体が無音とみなされた";
            }

            AudioEditSettings settings = source.Previous?.Clone() ?? AudioEditSettings.CreateDefault(source.Duration);
            failed = false;
            if (System.Math.Abs(settings.TrimStart - start) < 0.0005 && System.Math.Abs(settings.TrimEnd - end) < 0.0005)
                return "そのまま（削る無音が無い）";

            settings.TrimStart = start;
            settings.TrimEnd = end;
            if (!AudioClipEditing.TryApply(source, settings, out error))
            {
                failed = true;
                Debug.LogError($"[{Title}] {assetPath}\n{error}");
                return "失敗　書き出しに失敗（Console に詳細）";
            }

            changed = true;
            return $"削った　{source.Duration:0.000} 秒 → {settings.OutputDuration:0.000} 秒";
        }

        private static List<string> CollectSelectedPaths()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets)
                .Select(AssetDatabase.GetAssetPath)
                .Where(AudioClipEditing.IsSupported)
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }
    }
}
