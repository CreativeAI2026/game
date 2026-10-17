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
    /// ffmpeg / ffprobe を呼んで、音声ファイルを調べる・測る・加工して書き出す・波形データとして読み出す。
    /// 音量をそろえるツール（AudioLoudnessNormalizer）と音声エディタ（AudioClipEditWindow）が使う。
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

        // ffmpeg に入っているフィルターの一覧。毎回 ffmpeg を呼ぶと遅いため、一度だけ調べて覚えておく
        private static string _filterList;

        /// <summary>
        /// ffmpeg にそのフィルターが入っているか。配布物によって入っているフィルターが違う
        /// （例：rubberband は full 版には入っているが、軽量版には入っていない）。
        /// </summary>
        public static bool HasFilter(string name)
        {
            if (_filterList == null)
            {
                if (FfmpegPath == null) return false;
                Run(FfmpegPath, "-hide_banner -nostdin -filters", out string stdout, out _);
                _filterList = stdout ?? string.Empty;
            }
            return Regex.IsMatch(_filterList, @"\s" + Regex.Escape(name) + @"\s");
        }

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

        /// <summary>音声の形式名・サンプリング周波数・チャンネル数を調べる。調べられなければ false。</summary>
        public static bool TryProbeFormat(string filePath, out string codecName, out int sampleRate, out int channels)
        {
            codecName = null;
            sampleRate = 0;
            channels = 0;

            string args = $"-v error -select_streams a:0 -show_entries stream=codec_name,sample_rate,channels -of default=nw=1 {Quote(filePath)}";
            if (Run(FfprobePath, args, out string stdout, out _) != 0) return false;

            foreach (string line in stdout.Split('\n').Select(l => l.Trim()))
            {
                if (line.StartsWith("codec_name=")) codecName = line.Substring("codec_name=".Length);
                else if (line.StartsWith("sample_rate=")) int.TryParse(line.Substring("sample_rate=".Length), out sampleRate);
                else if (line.StartsWith("channels=")) int.TryParse(line.Substring("channels=".Length), out channels);
            }
            return codecName != null && sampleRate > 0 && channels > 0;
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
            return TryRenderFiltered(inputPath, outputPath, filter, encoderArgs, out error);
        }

        /// <summary>
        /// ffmpeg のフィルター（-af に渡す文字列。例："atrim=start=0.1,asetpts=PTS-STARTPTS"）を掛けて outputPath に書き出す。
        /// encoderArgs は書き出す形式の指定。失敗したら ffmpeg の出力を error に返す。
        /// </summary>
        public static bool TryRenderFiltered(string inputPath, string outputPath, string filter, string encoderArgs, out string error)
        {
            // -map 0:a:0：音だけを書き出す（mp3 に入っているジャケット画像などは捨てる）
            string args = $"-hide_banner -nostats -nostdin -y -i {Quote(inputPath)} -map 0:a:0{FilterArg(filter)} {encoderArgs} {Quote(outputPath)}";
            int exitCode = Run(FfmpegPath, args, out _, out string stderr);
            error = exitCode == 0 ? null : stderr;
            return exitCode == 0;
        }

        /// <summary>
        /// 音声を（フィルターを掛けたうえで）波形データとして読み出す。
        /// samples はチャンネルが交互に並んだ -1〜1 の値。チャンネル数・サンプリング周波数は指定した値にそろえる。
        /// </summary>
        public static bool TryDecode(string inputPath, string filter, int channels, int sampleRate, out float[] samples, out string error)
        {
            samples = null;
            string args = $"-hide_banner -nostats -nostdin -i {Quote(inputPath)} -map 0:a:0{FilterArg(filter)} " +
                          $"-ac {channels} -ar {sampleRate} -f f32le -c:a pcm_f32le -";
            int exitCode = RunBinary(FfmpegPath, args, out byte[] stdout, out string stderr);
            if (exitCode != 0)
            {
                error = stderr;
                return false;
            }

            error = null;
            samples = new float[stdout.Length / sizeof(float)];
            Buffer.BlockCopy(stdout, 0, samples, 0, samples.Length * sizeof(float));
            return true;
        }

        /// <summary>
        /// 元と同じ形式で書き出す指定。未対応の形式なら null。
        /// mp3・ogg は書き出し直しで劣化するため、高い音質で書き出す。wav は元と同じ PCM の形式のまま書き出す。
        /// </summary>
        public static string GetEncoderArgs(string extension, string codecName)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".mp3": return "-c:a libmp3lame -b:a 320k";
                case ".ogg": return "-c:a libvorbis -q:a 8";
                case ".wav": return codecName != null && codecName.StartsWith("pcm_") ? "-c:a " + codecName : null;
                default: return null;
            }
        }

        private static string FilterArg(string filter) => string.IsNullOrEmpty(filter) ? string.Empty : " -af " + Quote(filter);

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

        /// <summary>標準出力をバイト列のまま受け取る版（波形データの読み出し用）。</summary>
        private static int RunBinary(string exePath, string args, out byte[] stdout, out string stderr)
        {
            var startInfo = new ProcessStartInfo(exePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var process = Process.Start(startInfo))
            using (var buffer = new MemoryStream())
            {
                var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(buffer);
                stderr = process.StandardError.ReadToEnd();
                stdoutTask.Wait();
                process.WaitForExit();
                stdout = buffer.ToArray();
                return process.ExitCode;
            }
        }
    }
}
