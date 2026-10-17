using System.IO;
using UnityEditor;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタとまとめて処理するメニューが共通で使う、素材の読み込み・試聴用の加工・保存。
    /// 加工はすべて ffmpeg で行う。対応する形式は、音量をそろえるツールと同じ mp3・ogg・wav。
    /// </summary>
    public static class AudioClipEditing
    {
        /// <summary>編集の元にする素材を読み込んだもの。</summary>
        public sealed class Source
        {
            public string AssetPath;

            /// <summary>編集の元にする素材のファイル（素材そのもの、または保存しておいた編集前・音量をそろえる前の素材）。</summary>
            public string FilePath;

            public string CodecName;
            public int SampleRate;
            public int Channels;

            /// <summary>チャンネルが交互に並んだ波形データ（-1〜1）。</summary>
            public float[] Samples;

            /// <summary>前回保存した編集内容。初めて編集する素材なら null。</summary>
            public AudioEditSettings Previous;

            public double Duration => Samples.Length / (double)(Channels * SampleRate);
        }

        public static bool IsFfmpegAvailable => FfmpegAudio.FfmpegPath != null && FfmpegAudio.FfprobePath != null;

        /// <summary>音程・速度の変更に、質の良い rubberband を使えるか（無ければ atempo で代用する）。</summary>
        public static bool UsesRubberband => FfmpegAudio.HasFilter("rubberband");

        public static bool IsSupported(string assetPath)
        {
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            return assetPath.StartsWith("Assets/")
                   && AssetImporter.GetAtPath(assetPath) is AudioImporter
                   && (extension == ".mp3" || extension == ".ogg" || extension == ".wav");
        }

        public static bool TryLoad(string assetPath, out Source source, out string error)
        {
            source = null;
            if (!IsFfmpegAvailable)
            {
                error = "ffmpeg（ffmpeg.exe と ffprobe.exe）が見つかりません。インストール済みの場合は、Unity を開き直してください。";
                return false;
            }
            if (!IsSupported(assetPath))
            {
                error = "対応していない素材です（mp3・ogg・wav のみ）。";
                return false;
            }

            string filePath = AudioEditStore.ResolveSource(assetPath, out AudioEditSettings previous);
            if (!FfmpegAudio.TryProbeFormat(filePath, out string codecName, out int sampleRate, out int channels))
            {
                error = "形式を読み取れませんでした。";
                return false;
            }
            if (!FfmpegAudio.TryDecode(filePath, null, channels, sampleRate, out float[] samples, out string decodeError))
            {
                error = "読み込みに失敗しました。\n" + decodeError;
                return false;
            }

            source = new Source
            {
                AssetPath = assetPath,
                FilePath = filePath,
                CodecName = codecName,
                SampleRate = sampleRate,
                Channels = channels,
                Samples = samples,
                Previous = previous,
            };
            source.Previous?.Clamp(source.Duration);
            error = null;
            return true;
        }

        /// <summary>
        /// 読み込んだときのファイルが今もあるか。ffmpeg は日本語を含むパスのファイルが無いと「Illegal byte sequence」という
        /// 分かりにくい表示を出すため、先に確かめて理由を伝える。
        /// </summary>
        private static bool SourceExists(Source source, out string error)
        {
            bool exists = File.Exists(source.FilePath) && File.Exists(Path.GetFullPath(source.AssetPath));
            error = exists
                ? null
                : $"ファイルが見つかりません（名前の変更・移動・削除をした可能性があります）。素材を開き直してください。\n{source.AssetPath}";
            return exists;
        }

        /// <summary>保存したときと同じ加工を掛けた波形データを作る（試聴用）。</summary>
        public static bool TryRenderPreview(Source source, AudioEditSettings settings, out float[] samples, out string error)
        {
            samples = null;
            if (!SourceExists(source, out error)) return false;

            string filter = settings.BuildFilter(source.Duration, source.SampleRate, UsesRubberband);
            return FfmpegAudio.TryDecode(source.FilePath, filter, source.Channels, source.SampleRate, out samples, out error);
        }

        /// <summary>編集内容を掛けて素材を上書きする。元のファイルは AudioEditStore が保存する。</summary>
        public static bool TryApply(Source source, AudioEditSettings settings, out string error)
        {
            if (!SourceExists(source, out error)) return false;

            string encoderArgs = FfmpegAudio.GetEncoderArgs(Path.GetExtension(source.AssetPath), source.CodecName);
            if (encoderArgs == null)
            {
                error = "この形式では書き出せません（wav は PCM 形式のみ対応）。";
                return false;
            }

            string tempPath = Path.Combine(Path.GetFullPath("Temp"), "AudioEdit" + Path.GetExtension(source.AssetPath));
            string filter = settings.BuildFilter(source.Duration, source.SampleRate, UsesRubberband);
            if (!FfmpegAudio.TryRenderFiltered(source.FilePath, tempPath, filter, encoderArgs, out string renderError))
            {
                error = "書き出しに失敗しました。\n" + renderError;
                return false;
            }

            AudioEditStore.Commit(source.AssetPath, source.FilePath, tempPath, settings.Clone());
            File.Delete(tempPath);
            error = null;
            return true;
        }
    }
}
