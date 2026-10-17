using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音素材ごとにバラバラな音量を、聞こえ方の大きさ（ラウドネス）でそろえる。音声ファイルそのものを書き換える。
    /// 素材を足したら、メニューから実行し直せばよい。処理済みの素材は飛ばすので、何度実行してもよい。
    ///
    /// ・対象：ビルドに入るシーン・Resources・全 SoundCue / UISoundSet から使われている音素材（またはプロジェクトで選んだ音素材）
    /// ・やること：音量を上げ下げするだけ。音色は変えない（大きい瞬間を抑える処理はしない）
    /// ・そろえる大きさは基準（TargetLufs）に、その素材を入れた SoundCue の「素材の大きさ（dB）」を足したもの
    /// ・上げると音割れする素材は、SoundCue の「音割れしても上げる」がオフなら音割れの手前で止め、オンなら目標まで上げる
    /// ・元の素材はプロジェクト直下の AudioLoudnessBackup/Originals に1度だけ保存し、以後は毎回そこから作り直す（劣化が重ならないように）
    /// ・処理済みの印は .meta（AudioImporter.userData）に残す。ファイルを差し替えると印と合わなくなり、
    ///   次の実行で差し替えた素材を新しい元の素材として処理する
    ///
    /// 用途ごとの音量差は、SoundCue の「素材の大きさ（dB）」（上げる・下げる）と Volume（下げるだけ）、AudioMixer のグループで付ける。
    /// </summary>
    public static class AudioLoudnessNormalizer
    {
        /// <summary>
        /// そろえる基準の大きさ（LUFS）。効果音で短い大きな音（足音・衝撃音）が多いため、上げても音割れしにくいよう控えめにしている。
        /// 変えると全素材が処理し直しになる。
        /// </summary>
        public const double TargetLufs = -20;

        /// <summary>音割れを避ける場合の、一番大きい瞬間の上限（dB）。0 を超えると音割れするため少し余裕を持たせる。</summary>
        private const double PeakCeilingDb = -1;

        /// <summary>音量の変化がこれより小さい素材は書き出し直さない（mp3 などは書き出すたびに少し劣化するため）。</summary>
        private const double MinChangeDb = 0.5;

        /// <summary>印の形式。作り方を変えたときに上げると、全素材が元の素材から作り直される。</summary>
        private const string MarkerPrefix = "LoudnessNormalized";
        private const int MarkerVersion = 2;

        private const string OriginalsFolder = "AudioLoudnessBackup/Originals";

        /// <summary>素材をどう作るか。SoundCue の設定から決まる。</summary>
        public readonly struct ClipSetting
        {
            public readonly double Target;
            public readonly bool AllowClipping;

            public ClipSetting(double target, bool allowClipping)
            {
                Target = target;
                AllowClipping = allowClipping;
            }
        }

        private enum Outcome { Adjusted, AlreadyClose, Failed }

        private readonly struct Result
        {
            public readonly string Path;
            public readonly Outcome Outcome;
            public readonly ClipSetting Setting;
            public readonly double Before;
            public readonly double After;
            public readonly double ClippedDb;
            public readonly bool ShortOfTarget;
            public readonly string Note;

            public Result(string path, Outcome outcome, ClipSetting setting, double before = double.NaN, double after = double.NaN,
                double clippedDb = 0, bool shortOfTarget = false, string note = null)
            {
                Path = path;
                Outcome = outcome;
                Setting = setting;
                Before = before;
                After = after;
                ClippedDb = clippedDb;
                ShortOfTarget = shortOfTarget;
                Note = note;
            }
        }

        [MenuItem("Tools/SoundKit/ゲームで使う音素材の音量をそろえる")]
        private static void NormalizeUsedClips()
        {
            Normalize(CollectUsedClipPaths(), askConfirmation: true);
        }

        [MenuItem("Assets/SoundKit/選択した音素材の音量をそろえる")]
        private static void NormalizeSelectedClips()
        {
            Normalize(CollectSelectedClipPaths(), askConfirmation: true);
        }

        [MenuItem("Assets/SoundKit/選択した音素材の音量をそろえる", true)]
        private static bool CanNormalizeSelectedClips() => CollectSelectedClipPaths().Count > 0;

        /// <summary>指定した素材だけをそろえる（SoundCue のインスペクタの「反映」ボタン用）。確認画面は出さない。</summary>
        public static void NormalizeClips(IEnumerable<string> clipPaths)
        {
            Normalize(clipPaths.Where(IsAudioAsset).Distinct().ToList(), askConfirmation: false);
        }

        /// <summary>素材が今の SoundCue の設定どおりに作られているか。</summary>
        public static bool IsUpToDate(string clipPath, ClipSetting setting)
        {
            var importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
            return importer != null && importer.userData == CreateMarker(Path.GetFullPath(clipPath), setting);
        }

        /// <summary>素材ごとの作り方。素材を入れた全 SoundCue の設定から決める。</summary>
        public static Dictionary<string, ClipSetting> CollectSettings(List<string> conflicts = null)
        {
            var settings = new Dictionary<string, ClipSetting>();
            foreach (string cuePath in FindAssetPaths("t:SoundCue"))
            {
                var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(cuePath);
                if (cue == null) continue;

                var cueSetting = new ClipSetting(Math.Round(TargetLufs + cue.LoudnessOffsetDb, 1), cue.AllowClipping);
                foreach (AudioClip clip in cue.Clips.Where(clip => clip != null))
                {
                    string clipPath = AssetDatabase.GetAssetPath(clip);
                    if (!settings.TryGetValue(clipPath, out ClipSetting existing))
                    {
                        settings[clipPath] = cueSetting;
                        continue;
                    }

                    // 設定の違う複数の SoundCue に入っている素材は、大きくする側に合わせる
                    if (Math.Abs(existing.Target - cueSetting.Target) < 0.05 && existing.AllowClipping == cueSetting.AllowClipping) continue;
                    conflicts?.Add(Path.GetFileName(clipPath));
                    settings[clipPath] = new ClipSetting(Math.Max(existing.Target, cueSetting.Target),
                        existing.AllowClipping || cueSetting.AllowClipping);
                }
            }
            return settings;
        }

        public static ClipSetting GetSetting(Dictionary<string, ClipSetting> settings, string clipPath)
            => settings.TryGetValue(clipPath, out ClipSetting setting) ? setting : new ClipSetting(TargetLufs, false);

        /// <summary>ゲームで鳴る可能性のある音素材。素材パックのデモシーンだけで使われている音は含めない。</summary>
        private static List<string> CollectUsedClipPaths()
        {
            var roots = new List<string>();
            roots.AddRange(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
            roots.AddRange(FindAssetPaths("t:SoundCue"));
            roots.AddRange(FindAssetPaths("t:UISoundSet"));
            // Resources は名前で読み込まれるため、シーンから参照されていなくても鳴る
            roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.Contains("/Resources/")));

            return AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true)
                .Where(IsAudioAsset)
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        private static List<string> CollectSelectedClipPaths()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets)
                .Select(AssetDatabase.GetAssetPath)
                .Where(IsAudioAsset)
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        private static IEnumerable<string> FindAssetPaths(string filter)
            => AssetDatabase.FindAssets(filter).Select(AssetDatabase.GUIDToAssetPath);

        private static bool IsAudioAsset(string path)
            => path.StartsWith("Assets/") && AssetImporter.GetAtPath(path) is AudioImporter;

        private static void Normalize(List<string> clipPaths, bool askConfirmation)
        {
            if (FfmpegAudio.FfmpegPath == null || FfmpegAudio.FfprobePath == null)
            {
                EditorUtility.DisplayDialog("音量をそろえる",
                    "ffmpeg（ffmpeg.exe と ffprobe.exe）が見つかりません。\n" +
                    "インストール済みの場合は、Unity を開き直してから実行してください。", "OK");
                return;
            }

            if (clipPaths.Count == 0)
            {
                // 「すべて処理済み」と区別がつかないと、実行されなかったのか分からないため理由を出す
                const string message =
                    "対象の音素材が見つからないため、何もしませんでした。\n\n" +
                    "対象は「SoundCue に入っている素材」「ビルドに入るシーンが参照している素材」「Resources 内の素材」です。\n" +
                    "それ以外の素材は、Project ウィンドウで選んで右クリック > SoundKit > 選択した音素材の音量をそろえる で処理できます。";
                Debug.Log("[音量をそろえる] " + message);
                if (askConfirmation) EditorUtility.DisplayDialog("音量をそろえる", message, "OK");
                return;
            }

            var conflicts = new List<string>();
            Dictionary<string, ClipSetting> settings = CollectSettings(conflicts);

            var pending = clipPaths.Where(path => !IsUpToDate(path, GetSetting(settings, path))).ToList();
            int skippedCount = clipPaths.Count - pending.Count;
            if (pending.Count == 0)
            {
                Debug.Log($"[音量をそろえる] 対象の音素材 {clipPaths.Count} 個は、すべて処理済みのため何もしませんでした。");
                if (askConfirmation)
                {
                    EditorUtility.DisplayDialog("音量をそろえる",
                        $"対象の音素材 {clipPaths.Count} 個は、すべて処理済みです。", "OK");
                }
                return;
            }

            if (askConfirmation && !EditorUtility.DisplayDialog("音量をそろえる",
                    $"音素材 {pending.Count} 個の音量をそろえます（処理済みの {skippedCount} 個は飛ばします）。\n\n" +
                    $"音声ファイルを書き換えます。初めて処理する素材は、元のファイルをプロジェクト直下の {OriginalsFolder} に保存します。",
                    "実行", "やめる"))
            {
                return;
            }

            var results = new List<Result>();
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    string path = pending[i];
                    if (EditorUtility.DisplayCancelableProgressBar("音量をそろえる",
                            $"{i + 1}/{pending.Count}  {Path.GetFileName(path)}", (float)i / pending.Count))
                    {
                        break;
                    }
                    results.Add(NormalizeClip(path, GetSetting(settings, path)));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LogReport(results, skippedCount, conflicts);
            if (!askConfirmation) return;

            int failedCount = results.Count(result => result.Outcome == Outcome.Failed);
            int shortCount = results.Count(result => result.ShortOfTarget);
            EditorUtility.DisplayDialog("音量をそろえる",
                $"処理：{results.Count} 個（うち失敗 {failedCount} 個、音割れの手前で止めて目標に届かなかった {shortCount} 個）\n" +
                "素材ごとの結果は Console に出しました。", "OK");
        }

        private static Result NormalizeClip(string assetPath, ClipSetting setting)
        {
            string filePath = Path.GetFullPath(assetPath);
            var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
            if (!string.IsNullOrEmpty(importer.userData) && !importer.userData.StartsWith(MarkerPrefix))
            {
                // 他の仕組みが userData を使っている素材。印を書くと消してしまうため触らない
                return new Result(assetPath, Outcome.Failed, setting, note: "userData が別の用途で使われているため飛ばした");
            }

            string originalPath = EnsureOriginal(assetPath, filePath, importer.userData);

            if (!FfmpegAudio.TryProbe(originalPath, out string codecName, out double duration))
                return new Result(assetPath, Outcome.Failed, setting, note: "形式・長さを読めない");

            string encoderArgs = FfmpegAudio.GetEncoderArgs(Path.GetExtension(assetPath), codecName);
            if (encoderArgs == null)
                return new Result(assetPath, Outcome.Failed, setting, note: $"未対応の形式（{Path.GetExtension(assetPath)}）");

            if (!FfmpegAudio.TryMeasure(originalPath, duration, out FfmpegAudio.Loudness before))
                return new Result(assetPath, Outcome.Failed, setting, note: "無音などで大きさを測れない");

            double gainDb = setting.Target - before.Integrated;
            double overCeilingDb = before.TruePeak + gainDb - PeakCeilingDb;
            bool shortOfTarget = !setting.AllowClipping && overCeilingDb > 0;
            if (shortOfTarget) gainDb -= overCeilingDb;
            double clippedDb = Math.Max(0, before.TruePeak + gainDb);

            if (Math.Abs(gainDb) < MinChangeDb)
            {
                // 元の素材のままでよい。以前に別の大きさで作り直していた場合は元に戻す
                File.Copy(originalPath, filePath, true);
                MarkNormalized(importer, filePath, setting);
                return new Result(assetPath, Outcome.AlreadyClose, setting, before.Integrated, before.Integrated,
                    clippedDb, shortOfTarget);
            }

            string tempPath = Path.Combine(Path.GetFullPath("Temp"), "AudioLoudness" + Path.GetExtension(assetPath));
            if (!FfmpegAudio.TryRender(originalPath, tempPath, gainDb, encoderArgs, out string error))
            {
                Debug.LogError($"[音量をそろえる] {assetPath} の書き出しに失敗しました。\n{error}");
                return new Result(assetPath, Outcome.Failed, setting, before.Integrated, note: "書き出しに失敗（Console に詳細）");
            }

            File.Copy(tempPath, filePath, true);
            File.Delete(tempPath);

            double after = FfmpegAudio.TryMeasure(filePath, duration, out FfmpegAudio.Loudness measured)
                ? measured.Integrated
                : double.NaN;
            MarkNormalized(importer, filePath, setting);
            return new Result(assetPath, Outcome.Adjusted, setting, before.Integrated, after, clippedDb, shortOfTarget);
        }

        /// <summary>
        /// 作り直しの元にする素材の場所。今のファイルがこのツールの書き出したもの（印の md5 と一致）なら保存済みの元の素材を使う。
        /// 初めて処理する素材や、差し替えられた素材は、今のファイルを元の素材として保存する。
        /// </summary>
        private static string EnsureOriginal(string assetPath, string filePath, string userData)
        {
            string originalPath = Path.GetFullPath(Path.Combine(OriginalsFolder, assetPath));
            bool isOurOutput = TryGetMarkerMd5(userData, out string markerMd5) && markerMd5 == ComputeMd5(filePath);
            if (isOurOutput && File.Exists(originalPath)) return originalPath;

            Directory.CreateDirectory(Path.GetDirectoryName(originalPath));
            File.Copy(filePath, originalPath, true);
            return originalPath;
        }

        /// <summary>
        /// 今のファイルがこのツールで音量をそろえたものなら、そろえる前の元の素材の場所を返す（音声エディタが、
        /// そろえた後の素材をさらに書き出し直して劣化を重ねないよう、元の素材から編集するために使う）。
        /// </summary>
        internal static bool TryGetNormalizedOriginal(string assetPath, out string originalPath)
        {
            originalPath = Path.GetFullPath(Path.Combine(OriginalsFolder, assetPath));
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            return importer != null
                   && TryGetMarkerMd5(importer.userData, out string markerMd5)
                   && markerMd5 == ComputeMd5(Path.GetFullPath(assetPath))
                   && File.Exists(originalPath);
        }

        /// <summary>
        /// 素材の名前変更・移動に合わせて、保存してある元の素材を新しいパスの場所へ移す（AudioBackupFollower が呼ぶ）。
        /// 処理済みの印は素材の .meta にあり素材と一緒に動くため、元の素材さえ移せば引き続き元から作り直せる。
        /// </summary>
        internal static void MoveOriginal(string fromAssetPath, string toAssetPath)
        {
            string from = Path.GetFullPath(Path.Combine(OriginalsFolder, fromAssetPath));
            if (!File.Exists(from)) return;

            string to = Path.GetFullPath(Path.Combine(OriginalsFolder, toAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        /// <summary>一度でもこのツールで音量をそろえた素材か（中身が変わって、そろえ直しが必要な場合も含む）。</summary>
        internal static bool WasNormalized(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            return importer != null && TryGetMarkerMd5(importer.userData, out _);
        }

        private static void MarkNormalized(AudioImporter importer, string filePath, ClipSetting setting)
        {
            importer.userData = CreateMarker(filePath, setting);
            importer.SaveAndReimport();
        }

        /// <summary>作り方とファイルの中身から作る印。どちらかが変わると処理し直しになる。</summary>
        private static string CreateMarker(string filePath, ClipSetting setting)
        {
            string target = setting.Target.ToString("0.0", CultureInfo.InvariantCulture);
            string clipping = setting.AllowClipping ? "1" : "0";
            return $"{MarkerPrefix};v={MarkerVersion};target={target};clip={clipping};md5={ComputeMd5(filePath)}";
        }

        private static bool TryGetMarkerMd5(string userData, out string md5)
        {
            md5 = null;
            if (string.IsNullOrEmpty(userData) || !userData.StartsWith(MarkerPrefix)) return false;

            const string key = "md5=";
            int index = userData.IndexOf(key, StringComparison.Ordinal);
            if (index < 0) return false;
            md5 = userData.Substring(index + key.Length);
            return true;
        }

        internal static string ComputeMd5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static void LogReport(List<Result> results, int skippedCount, List<string> conflicts)
        {
            var report = new StringBuilder();
            report.AppendLine($"[音量をそろえる] 基準 {TargetLufs} LUFS／処理 {results.Count} 個・処理済みで飛ばした {skippedCount} 個");
            report.AppendLine("（元 → 後 は聞こえ方の大きさ LUFS。「届かない」＝音割れの手前で止めた、「音割れ」＝音割れしても上げる設定で 0dB を超えた量）");
            if (conflicts.Count > 0)
            {
                report.AppendLine("※ 設定の違う複数の SoundCue に入っているため、大きくする側に合わせた素材：" +
                                  string.Join("、", conflicts.Distinct()));
            }

            foreach (Result result in results.OrderBy(result => result.Outcome).ThenBy(result => result.Path))
            {
                string name = Path.GetFileName(result.Path);
                string target = $"目標 {FormatLufs(result.Setting.Target)}";
                string flags = (result.ShortOfTarget ? "  届かない" : string.Empty)
                               + (result.ClippedDb > 0 ? $"  音割れ +{result.ClippedDb:0.0}dB" : string.Empty);
                switch (result.Outcome)
                {
                    case Outcome.Adjusted:
                        report.AppendLine($"調整　{target}　{FormatLufs(result.Before)} → {FormatLufs(result.After)}{flags}  {name}");
                        break;
                    case Outcome.AlreadyClose:
                        report.AppendLine($"そのまま　{target}　{FormatLufs(result.Before)}（目標に近いので元の素材のまま）{flags}  {name}");
                        break;
                    default:
                        report.AppendLine($"失敗　{result.Note}  {result.Path}");
                        break;
                }
            }
            Debug.Log(report.ToString());
        }

        private static string FormatLufs(double value)
            => double.IsNaN(value) ? "?" : value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
