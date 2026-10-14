using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 物体から鳴り続ける音（稼働音・テレビ・こする音など）の再生口。
    /// 音を出す物のスクリプトが持ち、オン・オフに合わせて Play / Stop を呼ぶ。
    ///
    /// コンポーネントにしていないのは、鳴らす音（SoundCue）を持ち主のスクリプトの欄に置き、
    /// プレハブへ別のコンポーネントを付け足す手間と付け忘れを無くすため。
    /// オン・オフは短くフェードさせ、ブツッと切れる音を避ける。
    /// </summary>
    public sealed class LoopingSound
    {
        public const float DefaultFadeDuration = 0.15f;

        private readonly AudioSource _source;
        private readonly AudioSourceFader _fader;
        private SoundCue _cue;

        public bool IsPlaying { get; private set; }

        /// <param name="host">音を出す物。ここに AudioSource を足し、この位置から鳴らす</param>
        public LoopingSound(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _fader = AudioSourceFader.AddTo(_source);
        }

        /// <summary>鳴らし始める。すでに同じ音が鳴っていれば何もしない。</summary>
        /// <param name="owner">未設定の警告に使う</param>
        public void Play(SoundCue cue, Object owner, float fadeDuration = DefaultFadeDuration)
        {
            if (IsPlaying && cue == _cue)
                return;

            if (cue == null || !cue.HasClip)
            {
                UnassignedSoundWarning.WarnOnce(owner, cue == null ? "鳴り続ける音" : $"音「{cue.name}」の素材");
                return;
            }

            IsPlaying = true;
            _cue = cue;

            _fader.Cancel();
            cue.ApplyTo(_source);
            _source.clip = cue.PickClip();
            _source.volume = 0f;
            _source.Play();
            _fader.FadeTo(cue.Volume, fadeDuration, stopOnComplete: false, useUnscaledTime: false);
        }

        public void Stop(float fadeDuration = DefaultFadeDuration)
        {
            if (!IsPlaying)
                return;

            IsPlaying = false;
            _fader.FadeTo(0f, fadeDuration, stopOnComplete: true, useUnscaledTime: false);
        }

        /// <summary>
        /// フェードせずすぐ止める。持ち主が無効化されるとき用（無効化中はフェードが進まず、鳴りかけのまま残るため）。
        /// </summary>
        public void StopImmediately()
        {
            IsPlaying = false;
            _fader.Cancel();
            _source.Stop();
        }
    }
}
