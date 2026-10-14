using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 場所全体に流れる環境音（空調・空気音・風など）を鳴らす入口。1つずつ鳴らし、切り替えはクロスフェードで行う。
    /// 音楽ではないため BGM とは分け、音量設定の「環境音」に従わせる（SoundCue の種類を「Ambient」にすること）。
    /// 特定の物から鳴る環境音（機械の稼働音など）は、その物から LoopingSound で鳴らす。
    /// </summary>
    public static class AmbiencePlayer
    {
        public const float DefaultFadeDuration = 1.5f;

        private static CrossfadeLoopTrack _track;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _track = null;

        /// <summary>環境音を鳴らす。同じ音が鳴っていれば何もしない。</summary>
        public static void Play(SoundCue cue, float fadeDuration = DefaultFadeDuration)
        {
            if (cue == null || !cue.HasClip)
            {
                UnassignedSoundWarning.WarnOnce(null, cue == null ? "場所の環境音" : $"環境音「{cue.name}」の素材");
                return;
            }

            GetTrack().Play(cue, fadeDuration);
        }

        public static void Stop(float fadeDuration = DefaultFadeDuration) => _track?.Stop(fadeDuration);

        private static CrossfadeLoopTrack GetTrack()
        {
            if (_track != null)
                return _track;

            var root = new GameObject("AmbiencePlayer");
            Object.DontDestroyOnLoad(root);
            _track = new CrossfadeLoopTrack(root.transform, "Ambience");
            return _track;
        }
    }
}
