using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SoundKit
{
    /// <summary>
    /// BGM を鳴らす段。Main の上に Overlay を重ねて鳴らせる（演出中だけ重ねる曲など）。
    /// </summary>
    public enum BgmLayer
    {
        Main,
        Overlay,
    }

    /// <summary>
    /// BGM（音楽）を鳴らす入口。段ごとに1曲を鳴らし、曲の切り替えはクロスフェードで行う。
    /// シーンをまたいで鳴らし続けられるよう、AudioSource はシーン外に置く。
    /// 場所の空気音のような環境音は AmbiencePlayer で鳴らす（音量設定の分類が違うため）。
    /// </summary>
    public static class BgmPlayer
    {
        public const float DefaultFadeDuration = 1f;

        private static CrossfadeLoopTrack[] _tracks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _tracks = null;

        /// <summary>
        /// 指定した段で曲を鳴らす。その段で同じ曲が鳴っていれば何もしない。
        /// </summary>
        public static void Play(
            SoundCue cue,
            BgmLayer layer = BgmLayer.Main,
            float fadeDuration = DefaultFadeDuration
        )
        {
            if (cue == null || !cue.HasClip)
            {
                UnassignedSoundWarning.WarnOnce(
                    null,
                    cue == null ? $"BGM（{layer}）" : $"BGM「{cue.name}」の素材"
                );
                return;
            }

            GetTrack(layer).Play(cue, fadeDuration);
        }

        public static void Stop(
            BgmLayer layer = BgmLayer.Main,
            float fadeDuration = DefaultFadeDuration
        )
        {
            if (_tracks == null)
                return;
            GetTrack(layer).Stop(fadeDuration);
        }

        public static void StopAll(float fadeDuration = DefaultFadeDuration)
        {
            if (_tracks == null)
                return;
            foreach (var track in _tracks)
                track.Stop(fadeDuration);
        }

        private static CrossfadeLoopTrack GetTrack(BgmLayer layer)
        {
            if (_tracks == null)
            {
                var root = new GameObject("BgmPlayer");
                Object.DontDestroyOnLoad(root);

                var layers = (BgmLayer[])Enum.GetValues(typeof(BgmLayer));
                _tracks = new CrossfadeLoopTrack[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                    _tracks[i] = new CrossfadeLoopTrack(root.transform, $"Bgm_{layers[i]}");
            }
            return _tracks[(int)layer];
        }
    }
}
