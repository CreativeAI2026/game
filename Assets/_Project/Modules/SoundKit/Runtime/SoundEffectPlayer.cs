using System.Collections.Generic;
using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 画面の音（知らせの音・UIの音）を鳴らす入口。鳴らしたい側はこれを呼ぶだけにする。
    /// ゲーム世界の位置から鳴らす音は WorldSoundPlayer を使う。
    ///
    /// ポーズなどで AudioListener.pause により世界の音を止めても、画面の音は聞こえる必要があるため、
    /// ここで鳴らす音はその停止の影響を受けないようにしている（ignoreListenerPause）。
    ///
    /// SoundCue の素材・音量・種類（流し先）を使う。音程の揺らぎ・立体音・距離は使わない
    /// （種類ごとに1つの AudioSource を共有して重ねて鳴らすため、音程を変えると鳴っている他の音まで変わってしまう）。
    /// </summary>
    public static class SoundEffectPlayer
    {
        private static readonly Dictionary<SoundCategory, AudioSource> Sources =
            new Dictionary<SoundCategory, AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Sources.Clear();

        /// <param name="cue">鳴らす音。未設定・素材が空の場合は null でもよい（初回だけ警告を出して鳴らさない）</param>
        /// <param name="owner">どこから鳴らそうとしたか。音が未設定のときの警告に使う</param>
        public static void Play(SoundCue cue, Object owner)
        {
            AudioClip clip = cue != null ? cue.PickClip() : null;
            if (clip == null)
            {
                UnassignedSoundWarning.WarnOnce(owner, cue == null ? null : $"音「{cue.name}」の素材");
                return;
            }

            EnsureSource(cue.Category).PlayOneShot(clip, cue.Volume);
        }

        private static AudioSource EnsureSource(SoundCategory category)
        {
            if (Sources.TryGetValue(category, out AudioSource existing) && existing != null)
                return existing;

            var go = new GameObject($"SoundEffectPlayer_{category}");
            Object.DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = AudioMixerRouting.GetGroup(category);
            Sources[category] = source;
            return source;
        }
    }
}
