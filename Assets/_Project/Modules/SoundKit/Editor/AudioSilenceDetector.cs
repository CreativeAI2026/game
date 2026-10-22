using System;
using UnityEditor;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 前後の無音（指定した大きさ以下が続く部分）を見つける。
    /// 基準（何 dB 以下を無音とみなすか・前後に残す余白）は、音声エディタとまとめて処理するメニューで共通の設定として保存する。
    /// </summary>
    public static class AudioSilenceDetector
    {
        public const float DefaultThresholdDb = -50f;

        /// <summary>前後に残す余白（秒）の既定値。音の立ち上がりが欠けないよう、ほんの少しだけ残す。</summary>
        public const float DefaultMarginSeconds = 0.01f;

        private const string ThresholdKey = "SoundKit.AudioEdit.SilenceThresholdDb";
        private const string MarginKey = "SoundKit.AudioEdit.SilenceMarginSeconds";

        /// <summary>この大きさ（dB）以下を無音とみなす。</summary>
        public static float ThresholdDb
        {
            get => EditorPrefs.GetFloat(ThresholdKey, DefaultThresholdDb);
            set => EditorPrefs.SetFloat(ThresholdKey, value);
        }

        /// <summary>無音を削ったあと、前後に残す余白（秒）。</summary>
        public static float MarginSeconds
        {
            get => EditorPrefs.GetFloat(MarginKey, DefaultMarginSeconds);
            set => EditorPrefs.SetFloat(MarginKey, Math.Max(0f, value));
        }

        /// <summary>
        /// 無音でない部分の始まりと終わり（秒、余白込み）を求める。全体が無音なら false。
        /// どれか1つのチャンネルでも基準を超えていれば音があるとみなす。
        /// </summary>
        public static bool TryFindSoundRange(
            float[] samples,
            int channels,
            int sampleRate,
            float thresholdDb,
            float marginSeconds,
            out double start,
            out double end
        )
        {
            float threshold = (float)Math.Pow(10, thresholdDb / 20.0);
            int frameCount = samples.Length / channels;

            int first = -1;
            for (int frame = 0; frame < frameCount && first < 0; frame++)
                if (IsLoud(samples, frame, channels, threshold))
                    first = frame;

            start = end = 0;
            if (first < 0)
                return false;

            int last = first;
            for (int frame = frameCount - 1; frame > first; frame--)
            {
                if (!IsLoud(samples, frame, channels, threshold))
                    continue;
                last = frame;
                break;
            }

            double duration = frameCount / (double)sampleRate;
            start = Math.Max(0, first / (double)sampleRate - marginSeconds);
            end = Math.Min(duration, (last + 1) / (double)sampleRate + marginSeconds);
            return true;
        }

        private static bool IsLoud(float[] samples, int frame, int channels, float threshold)
        {
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                if (Math.Abs(samples[offset + channel]) > threshold)
                    return true;
            return false;
        }
    }
}
