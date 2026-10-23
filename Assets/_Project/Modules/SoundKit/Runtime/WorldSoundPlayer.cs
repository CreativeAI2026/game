using System.Collections.Generic;
using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// ゲーム世界の指定した位置で、音を1回だけ鳴らす入口（足音・衝突・ヒットなど）。
    ///
    /// 音源の物体に AudioSource を持たせず、使い回し用の AudioSource をその位置へ置いて鳴らす。
    /// 鳴らした物体がすぐ消えても（壊れる・拾われる）音が途中で切れないようにするため。
    /// AudioSource.PlayClipAtPoint はミキサーの流し先を指定できず音量設定が効かないので使わない。
    ///
    /// ポーズなどの停止（AudioListener.pause）では、世界の音として一緒に止まる。
    /// </summary>
    public static class WorldSoundPlayer
    {
        /// <summary>
        /// 同時に鳴らせる数の上限。衝突が連鎖した場合などに AudioSource が際限なく増えるのを防ぐ。
        /// </summary>
        private const int MaxSources = 32;

        /// <summary>音の長さを音程で割るときの下限。音程 0 付近での割り算の発散を防ぐ。</summary>
        private const float MinPitchForDuration = 0.01f;

        private static readonly List<AudioSource> _sources = new List<AudioSource>();
        private static Transform _root;
        private static bool _exhaustedReported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _sources.Clear();
            _root = null;
            _exhaustedReported = false;
        }

        /// <param name="cue">鳴らす音。null や素材なしでもよい（初回だけ警告を出して鳴らさない）</param>
        /// <param name="position">鳴らす位置（ワールド座標）</param>
        /// <param name="owner">どこから鳴らしたか。未設定の警告に使う</param>
        /// <param name="volumeScale">SoundCue の音量に掛ける倍率（ぶつかった強さで大きさを変える等）</param>
        /// <returns>鳴らした音の長さ（秒）。鳴らさなかったら 0。続けて別の音を鳴らす時刻を決めるのに使う</returns>
        public static float Play(
            SoundCue cue,
            Vector3 position,
            Object owner,
            float volumeScale = 1f
        )
        {
            if (cue == null || !cue.HasClip)
            {
                UnassignedSoundWarning.WarnOnce(
                    owner,
                    cue == null ? null : $"音「{cue.name}」の素材"
                );
                return 0f;
            }

            AudioSource source = FindIdleSource();
            if (source == null)
            {
                if (!_exhaustedReported)
                {
                    _exhaustedReported = true;
                    Debug.LogWarning(
                        $"[音] 同時に鳴らせる数（{MaxSources}）を超えたため、一部の音を鳴らさなかった。",
                        owner
                    );
                }
                return 0f;
            }

            source.transform.position = position;
            cue.ApplyTo(source);
            source.volume *= Mathf.Clamp01(volumeScale);
            source.clip = cue.PickClip();
            source.Play();
            return source.clip.length / Mathf.Max(MinPitchForDuration, source.pitch);
        }

        private static AudioSource FindIdleSource()
        {
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                // シーン切り替えでは消えないが、念のため外部から消された分は捨てる
                if (_sources[i] == null)
                {
                    _sources.RemoveAt(i);
                    continue;
                }
                if (!_sources[i].isPlaying)
                    return _sources[i];
            }

            if (_sources.Count >= MaxSources)
                return null;

            var source = CreateSource();
            _sources.Add(source);
            return source;
        }

        private static AudioSource CreateSource()
        {
            if (_root == null)
            {
                var rootObject = new GameObject("WorldSoundPlayer");
                Object.DontDestroyOnLoad(rootObject);
                _root = rootObject.transform;
            }

            var go = new GameObject("WorldSound");
            go.transform.SetParent(_root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            return source;
        }
    }
}
