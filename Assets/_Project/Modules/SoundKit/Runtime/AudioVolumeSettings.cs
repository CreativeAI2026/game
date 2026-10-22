using System;
using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 音量設定（全体・種類ごと）の保持と反映。値は 0～1 で扱い、ミキサーへはデシベルに直して渡す。
    ///
    /// 遊びの進行ではなく好みの設定なので、セーブデータではなく PlayerPrefs に保存する
    /// （セーブを消したり最初から始めても音量は変わらない）。
    /// </summary>
    public static class AudioVolumeSettings
    {
        private const string PrefsKeyPrefix = "Volume.";
        private const float DefaultVolume = 1f;

        /// <summary>ミキサーの音量の下限（dB）。ミキサーの Volume 欄の最小値で、無音として扱われる。</summary>
        private const float SilentDecibel = -80f;

        private static bool _parameterErrorReported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _parameterErrorReported = false;

        /// <summary>
        /// 起動直後（Awake の時点など）の AudioMixer.SetFloat は反映されない事例が知られているため、1フレーム待ってから反映する。
        /// 外部ライブラリに頼らないよう、Unity 標準の Awaitable で待つ。async void なのは起動時の呼び捨て処理のため
        /// （例外は Unity の同期コンテキストが Console に出す）。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static async void ApplySavedVolumesOnStartup()
        {
            await Awaitable.NextFrameAsync();
            foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
                ApplyToMixer(channel, Get(channel));
        }

        /// <summary>保存されている音量（0～1）。</summary>
        public static float Get(VolumeChannel channel) =>
            PlayerPrefs.GetFloat(PrefsKeyPrefix + channel, DefaultVolume);

        /// <summary>音量（0～1）を変えてすぐ反映する。ファイルへの書き出しは Save で行う。</summary>
        public static void Set(VolumeChannel channel, float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefsKeyPrefix + channel, volume);
            ApplyToMixer(channel, volume);
        }

        /// <summary>
        /// 変えた音量をファイルへ書き出す。スライダーを動かすたびに書き出すと重いため、設定画面を閉じるときなどに呼ぶ。
        /// </summary>
        public static void Save() => PlayerPrefs.Save();

        private static void ApplyToMixer(VolumeChannel channel, float volume)
        {
            var routing = AudioMixerRouting.Instance;
            if (routing == null || routing.Mixer == null)
                return;

            string parameterName = routing.GetVolumeParameterName(channel);
            if (routing.Mixer.SetFloat(parameterName, ToDecibel(volume)))
                return;

            if (!_parameterErrorReported)
            {
                _parameterErrorReported = true;
                Debug.LogError(
                    $"[音] ミキサーに公開された音量「{parameterName}」が見つからない。Exposed Parameters の名前を確認すること。",
                    routing
                );
            }
        }

        /// <summary>
        /// 0～1 の値をデシベルに直す。耳は音の大きさを対数で感じるため、
        /// 値をそのまま dB にするとスライダーの下半分でほぼ無音になる。
        /// </summary>
        private static float ToDecibel(float volume)
        {
            if (volume <= 0f)
                return SilentDecibel;
            return Mathf.Max(SilentDecibel, 20f * Mathf.Log10(volume));
        }
    }
}
