using System;
using System.Collections.Generic;
using System.Globalization;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタでの編集内容。切り取り位置は元の素材の時間（秒）で持つ。
    /// 保存した内容は編集の記録（AudioEditStore）に残し、次に開いたときは編集前の素材にこの内容を掛け直した状態から再開する
    /// （書き出した素材を再度加工すると、mp3・ogg では劣化が重なるため）。
    /// </summary>
    [Serializable]
    public class AudioEditSettings
    {
        public const float DefaultFadeMs = 5f;
        public const float MinSpeed = 0.5f;
        public const float MaxSpeed = 2f;
        public const float MaxPitchSemitones = 12f;

        // 端を「切り取った」とみなす差（秒）。浮動小数の誤差で、切っていない端にフェードが掛かるのを防ぐ
        private const double EdgeEpsilon = 0.0005;

        // ffmpeg の atempo は古い版だと 0.5〜2 倍しか受け付けないため、範囲外の倍率は分けて重ねる
        private const double MinAtempo = 0.5;
        private const double MaxAtempo = 2.0;

        public double TrimStart;
        public double TrimEnd;
        public float PitchSemitones;
        public float Speed = 1f;
        public float FadeInMs = DefaultFadeMs;
        public float FadeOutMs = DefaultFadeMs;

        /// <summary>何も変えない編集内容（素材の全体をそのまま使う）。</summary>
        public static AudioEditSettings CreateDefault(double sourceDuration) => new AudioEditSettings { TrimEnd = sourceDuration };

        public AudioEditSettings Clone() => (AudioEditSettings)MemberwiseClone();

        /// <summary>書き出した後の長さ（秒）。</summary>
        public double OutputDuration => Math.Max(0, TrimEnd - TrimStart) / Speed;

        public bool IsTrimmedStart => TrimStart > EdgeEpsilon;
        public bool IsTrimmedEnd(double sourceDuration) => TrimEnd < sourceDuration - EdgeEpsilon;

        /// <summary>値を扱える範囲に収める（ウィンドウの入力・読み込んだ記録のどちらにも使う）。</summary>
        public void Clamp(double sourceDuration)
        {
            TrimStart = Math.Max(0, Math.Min(TrimStart, sourceDuration));
            TrimEnd = Math.Max(TrimStart, Math.Min(TrimEnd, sourceDuration));
            PitchSemitones = Math.Max(-MaxPitchSemitones, Math.Min(PitchSemitones, MaxPitchSemitones));
            Speed = Math.Max(MinSpeed, Math.Min(Speed, MaxSpeed));
            FadeInMs = Math.Max(0, FadeInMs);
            FadeOutMs = Math.Max(0, FadeOutMs);
        }

        /// <summary>
        /// ffmpeg の -af に渡すフィルター。順に、切り取り → 切り取った端のフェード → 音程・速度。
        /// フェードは速度を変える前（元の素材の時間）で掛ける。速度を変えると前後がわずかにずれることがあり、
        /// 書き出した後の時間で位置を決めるとフェードがずれるため。
        ///
        /// 音程・速度は、ffmpeg に rubberband があればそれを打撃音向けの設定で使う（長さが正確で、音の始まりが鈍らない）。
        /// 無ければ、再生の速さごと音程を変えて（asetrate）から速さだけを atempo で戻す。
        /// atempo は無音から始まる音の始まりが前へずれる（音の手前に鳴りが出る）ことがあるため、代わりの手段としてだけ使う。
        /// </summary>
        public string BuildFilter(double sourceDuration, int sampleRate, bool useRubberband)
        {
            var filters = new List<string>();

            if (IsTrimmedStart || IsTrimmedEnd(sourceDuration))
            {
                filters.Add($"atrim=start={F(TrimStart)}:end={F(TrimEnd)}");
                // 切り取った後の時刻を 0 から数え直す（しないとフェードの時刻がずれる）
                filters.Add("asetpts=PTS-STARTPTS");
            }

            // フェードの長さは書き出した後の時間で指定されているので、元の素材の時間に直す
            double keptDuration = Math.Max(0, TrimEnd - TrimStart);
            double fadeIn = Math.Min(FadeInMs / 1000.0 * Speed, keptDuration);
            double fadeOut = Math.Min(FadeOutMs / 1000.0 * Speed, keptDuration);
            if (IsTrimmedStart && fadeIn > 0)
                filters.Add($"afade=t=in:st=0:d={F(fadeIn)}");
            if (IsTrimmedEnd(sourceDuration) && fadeOut > 0)
                filters.Add($"afade=t=out:st={F(keptDuration - fadeOut)}:d={F(fadeOut)}");

            double pitchRatio = Math.Pow(2, PitchSemitones / 12.0);
            bool changesPitch = Math.Abs(PitchSemitones) > 0.001f;
            bool changesSpeed = Math.Abs(Speed - 1f) > 0.0001f;
            if (useRubberband)
            {
                if (changesPitch || changesSpeed)
                {
                    // detector=percussive・window=short：効果音の短い立ち上がりを、ぼかさず前にもずらさずに残す設定
                    filters.Add($"rubberband=tempo={F(Speed)}:pitch={F(pitchRatio)}:transients=crisp:detector=percussive:window=short");
                }
                return string.Join(",", filters);
            }

            if (changesPitch)
            {
                int shiftedRate = (int)Math.Round(sampleRate * pitchRatio);
                filters.Add($"asetrate={shiftedRate}");
                filters.Add($"aresample={sampleRate}");
            }

            // 音程を上げた分だけ速くなっているので、それを打ち消したうえで指定の速さにする
            double tempo = Speed / pitchRatio;
            foreach (double factor in SplitTempo(tempo)) filters.Add($"atempo={F(factor)}");

            return string.Join(",", filters);
        }

        private static IEnumerable<double> SplitTempo(double tempo)
        {
            if (Math.Abs(tempo - 1.0) < 0.0001) yield break;

            while (tempo > MaxAtempo)
            {
                yield return MaxAtempo;
                tempo /= MaxAtempo;
            }
            while (tempo < MinAtempo)
            {
                yield return MinAtempo;
                tempo /= MinAtempo;
            }
            yield return tempo;
        }

        private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
