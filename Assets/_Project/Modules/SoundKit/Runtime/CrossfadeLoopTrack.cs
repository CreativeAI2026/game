using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 1曲ずつループで鳴らし、曲の切り替えをクロスフェードで行う再生口。BGM と場所全体の環境音が使う。
    /// クロスフェードのため AudioSource を2つ持ち、交互に使う。
    ///
    /// フェードは時間の停止（Time.timeScale = 0）の影響を受けない。
    /// 音そのものはポーズ等の AudioListener.pause で世界の音と一緒に止まる。
    /// </summary>
    public sealed class CrossfadeLoopTrack
    {
        private const int SourceCount = 2;

        private readonly AudioSource[] _sources = new AudioSource[SourceCount];
        private readonly AudioSourceFader[] _faders = new AudioSourceFader[SourceCount];
        private int _activeIndex;
        private SoundCue _currentCue;

        /// <param name="parent">AudioSource を置く親（シーンをまたいで残すなら DontDestroyOnLoad の物）</param>
        /// <param name="name">ヒエラルキーで見分けるための名前</param>
        public CrossfadeLoopTrack(Transform parent, string name)
        {
            for (int i = 0; i < SourceCount; i++)
            {
                var go = new GameObject($"{name}_{i}");
                go.transform.SetParent(parent, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                _sources[i] = source;
                _faders[i] = AudioSourceFader.AddTo(source);
            }
        }

        /// <summary>
        /// 曲を鳴らす。同じ曲が鳴っていれば何もしない（同じ場所に入り直しても頭から戻らない）。
        /// </summary>
        public void Play(SoundCue cue, float fadeDuration)
        {
            if (cue == _currentCue && _sources[_activeIndex].isPlaying)
                return;

            FadeOutAndStop(_activeIndex, fadeDuration);

            _activeIndex = 1 - _activeIndex;
            _currentCue = cue;

            var next = _sources[_activeIndex];
            _faders[_activeIndex].Cancel();
            cue.ApplyTo(next);
            // 聞く位置に関係なく同じ大きさで聞かせる
            next.spatialBlend = 0f;
            next.clip = cue.PickClip();
            next.volume = 0f;
            next.Play();
            _faders[_activeIndex].FadeTo(cue.Volume, fadeDuration, stopOnComplete: false, useUnscaledTime: true);
        }

        public void Stop(float fadeDuration)
        {
            _currentCue = null;
            for (int i = 0; i < SourceCount; i++)
                FadeOutAndStop(i, fadeDuration);
        }

        private void FadeOutAndStop(int index, float fadeDuration)
        {
            _faders[index].Cancel();
            if (!_sources[index].isPlaying)
                return;
            _faders[index].FadeTo(0f, fadeDuration, stopOnComplete: true, useUnscaledTime: true);
        }
    }
}
