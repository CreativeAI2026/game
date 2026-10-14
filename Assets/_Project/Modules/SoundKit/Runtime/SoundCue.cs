using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SoundKit
{
    /// <summary>
    /// 1つの「音」の定義。鳴らす素材（複数なら毎回ランダム）、音量、音程の揺らぎ、種類（流し先）、聞こえる距離を持つ。
    /// 鳴らす側はこれを受け取るだけにし、AudioSource の設定をあちこちに散らさない。
    /// 同じ音は1つの SoundCue を複数の物で使い回してよい。
    /// </summary>
    [CreateAssetMenu(menuName = "SoundKit/SoundCue", fileName = "NewSoundCue")]
    public class SoundCue : ScriptableObject
    {
        [Tooltip("鳴らす音。複数入れると毎回ランダムに選ぶ（同じ音が続かないようにする）")]
        [SerializeField]
        private AudioClip[] _clips;

        [SerializeField]
        [Range(0f, 1f)]
        private float _volume = 1f;

        [Tooltip(
            "素材そのものの大きさを、そろえる基準から何 dB 上げ下げするか。Volume は 1 より上げられないため、"
                + "他より大きく聞かせたい音はここで上げる。"
                + "変えたら Tools > SoundKit > ゲームで使う音素材の音量をそろえる（またはこの画面の「設定を素材に反映」）で素材に反映される"
        )]
        [SerializeField]
        [Range(-20f, 15f)]
        private float _loudnessOffsetDb;

        [Tooltip(
            "オン：音割れしても、上の大きさまで素材を上げる。"
                + "オフ：音割れする手前で上げるのを止める（目標の大きさに届かないことがある）。音色はどちらも変えない"
        )]
        [SerializeField]
        private bool _allowClipping;

        [Tooltip("鳴らすたびに音程をこの幅でずらす（0.05 なら ±5%）。足音などの単調さを消すため")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float _pitchVariation;

        [Tooltip("どの音量設定に従うか")]
        [SerializeField]
        private SoundCategory _category = SoundCategory.Effect;

        [Header("立体音（BGM・UI ではオフ）")]
        [Tooltip("オン：鳴った位置から聞こえる（離れると小さく、左右も分かる）。オフ：どこにいても同じに聞こえる")]
        [SerializeField]
        private bool _is3D = true;

        [Tooltip("この距離までは最大音量で聞こえる（m）")]
        [SerializeField]
        [Min(0.01f)]
        private float _minDistance = 1f;

        [Tooltip("この距離より遠いと、それ以上は小さくならない（m）")]
        [SerializeField]
        [Min(0.01f)]
        private float _maxDistance = 20f;

        /// <summary>直前に選んだ素材。複数あるときに同じ音が連続しないようにする。</summary>
        [NonSerialized]
        private int _lastClipIndex = -1;

        public SoundCategory Category => _category;
        public float Volume => _volume;
        public bool HasClip => _clips != null && _clips.Length > 0;

        /// <summary>素材の大きさの上げ下げ（dB）。音量をそろえるツールが使う（再生時には使わない）。</summary>
        public float LoudnessOffsetDb => _loudnessOffsetDb;

        /// <summary>音割れしても素材を目標の大きさまで上げるか。音量をそろえるツールが使う（再生時には使わない）。</summary>
        public bool AllowClipping => _allowClipping;

        /// <summary>入っている素材。音量をそろえるツールが使う。</summary>
        public IReadOnlyList<AudioClip> Clips => _clips ?? Array.Empty<AudioClip>();

        /// <summary>鳴らす素材を1つ選ぶ。素材が無ければ null。</summary>
        public AudioClip PickClip()
        {
            if (!HasClip)
                return null;
            if (_clips.Length == 1)
                return _clips[0];

            int index;
            if (_lastClipIndex < 0)
            {
                index = Random.Range(0, _clips.Length);
            }
            else
            {
                // 直前の1つを除いた中から選ぶ
                index = Random.Range(0, _clips.Length - 1);
                if (index >= _lastClipIndex)
                    index++;
            }
            _lastClipIndex = index;
            return _clips[index];
        }

        /// <summary>
        /// AudioSource を、この音の設定（流し先・音量・音程・立体音）にする。素材の差し替えと再生は呼び出し側で行う。
        /// </summary>
        public void ApplyTo(AudioSource source)
        {
            source.outputAudioMixerGroup = AudioMixerRouting.GetGroup(_category);
            source.volume = _volume;
            source.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
            source.spatialBlend = _is3D ? 1f : 0f;
            source.minDistance = _minDistance;
            source.maxDistance = _maxDistance;
        }

        private void OnValidate()
        {
            if (_maxDistance < _minDistance)
                _maxDistance = _minDistance;
        }
    }
}
