using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// ffmpeg / ffprobe を呼んで、音声ファイルの大きさ（ラウドネス）を測る・音量を変えて書き出す。
    /// 音量をそろえるツール（AudioLoudnessNormalizer）が使う。
    /// </summary>
    public static class FfmpegAudio
    {
        /// <summary>測った結果。Integrated は聞こえ方の大きさ（LUFS）、TruePeak は一番大きい瞬間（dBTP）。</summary>
        public readonly struct Loudness
        {
            public readonly double Integrated;
            public readonly double TruePeak;

            public Loudness(double integrated, double truePeak)
            {
                Integrated = integrated;
                TruePeak = truePeak;
            }
        }

        /// <summary>
        /// 聞こえ方の大きさは 0.4 秒ごとの区切りで測るため、短い音はそのままだと測れない（-inf になる）。
        /// この長さに満たない音は、繰り返しつないでから測る。
        /// </summary>
        private const double MinMeasureSeconds = 2.0;

        private static readonly Regex IntegratedPattern = new Regex("\"input_i\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex TruePeakPattern = new Regex("\"input_tp\"\\s*:\\s*\"([^\"]+)\"");

        public static string FfmpegPath => FindExecutable("ffmpeg");
        public static string FfprobePath => FindExecutable("ffprobe");

        /// <summary>
        /// ffmpeg の場所を探す。Unity を開いたあとに ffmpeg を入れた場合、Unity 自身の PATH には載っていないため、
        /// Windows に登録されたユーザー・システムの PATH も見る。
        /// </summary>
        private static string FindExecutable(string name)
        {
            var targets = new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine };
            var directories = targets
                .SelectMany(target => (Environment.GetEnvironmentVariable("PATH", target) ?? string.Empty).Split(Path.PathSeparator))
                .Select(directory => directory.Trim().Trim('"'))
                .Where(directory => directory.Length > 0);

            foreach (string directory in directories)
            {
                foreach (string fileName in new[] { name + ".exe", name })
                {
                    string candidate = Path.Combine(directory, fileName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        /// <summary>音声の形式名（mp3、pcm_s16le など）と長さ（秒）を調べる。調べられなければ false。</summary>
        public static bool TryProbe(string filePath, out string codecName, out double durationSeconds)
        {
            codecName = null;
            durationSeconds = 0;

            string args = $"-v error -select_streams a:0 -show_entries stream=codec_name:format=duration -of default=nw=1 {Quote(filePath)}";
            if (Run(FfprobePath, args, out string stdout, out _) != 0) return false;

            foreach (string line in stdout.Split('\n').Select(l => l.Trim()))
            {
                if (line.StartsWith("codec_name=")) codecName = line.Substring("codec_name=".Length);
                else if (line.StartsWith("duration=")) double.TryParse(line.Substring("duration=".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds);
            }
            return codecName != null && durationSeconds > 0;
        }

        /// <summary>聞こえ方の大きさを測る。無音などで測れなければ false。</summary>
        public static bool TryMeasure(string filePath, double durationSeconds, out Loudness loudness)
        {
            loudness = default;

            int loopCount = durationSeconds < MinMeasureSeconds
                ? (int)Math.Ceiling(MinMeasureSeconds / durationSeconds) - 1
                : 0;
            string loopArg = loopCount > 0 ? $"-stream_loop {loopCount} " : string.Empty;
            string args = $"-hide_banner -nostats -nostdin {loopArg}-i {Quote(filePath)} -map 0:a:0 -af loudnorm=print_format=json -f null -";
            if (Run(FfmpegPath, args, out _, out string stderr) != 0) return false;

            if (!TryParseValue(IntegratedPattern, stderr, out double integrated)) return false;
            if (!TryParseValue(TruePeakPattern, stderr, out double truePeak)) return false;

            loudness = new Loudness(integrated, truePeak);
            return true;
        }

        /// <summary>
        /// 音量を gainDb だけ変えて outputPath に書き出す。音色は変えない（大きい瞬間を抑える処理などはしない）。
        /// encoderArgs は書き出す形式の指定（例："-c:a libmp3lame -b:a 320k"）。失敗したら ffmpeg の出力を error に返す。
        /// </summary>
        public static bool TryRender(string inputPath, string outputPath, double gainDb, string encoderArgs, out string error)
        {
            string filter = "volume=" + gainDb.ToString("0.00", CultureInfo.InvariantCulture) + "dB";

            // -map 0:a:0：音だけを書き出す（mp3 に入っているジャケット画像などは捨てる）
            string args = $"-hide_banner -nostats -nostdin -y -i {Quote(inputPath)} -map 0:a:0 -af {filter} {encoderArgs} {Quote(outputPath)}";
            int exitCode = Run(FfmpegPath, args, out _, out string stderr);
            error = exitCode == 0 ? null : stderr;
            return exitCode == 0;
        }

        private static bool TryParseValue(Regex pattern, string text, out double value)
        {
            value = 0;
            Match match = pattern.Match(text);
            return match.Success
                   && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !double.IsInfinity(value);
        }

        private static string Quote(string path) => "\"" + path + "\"";

        private static int Run(string exePath, string args, out string stdout, out string stderr)
        {
            var startInfo = new ProcessStartInfo(exePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var process = Process.Start(startInfo))
            {
                // 片方だけ読んで待つと、もう片方の出力が詰まって止まることがあるため、同時に読む
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                stderr = process.StandardError.ReadToEnd();
                stdout = stdoutTask.Result;
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
