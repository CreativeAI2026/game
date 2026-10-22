using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 1つの AudioSource の音量を、時間をかけて目標の値へ変える（フェード）。
    /// 外部のトゥイーンライブラリに頼らず、どのプロジェクトへ持ち出しても動くようにするための部品。
    ///
    /// AudioSource と同じ GameObject に付け、その GameObject が消えればフェードも一緒に消える。
    /// フェード中だけ有効にし、何もしていない間は Update を回さない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class AudioSourceFader : MonoBehaviour
    {
        private AudioSource _source;
        private float _startVolume;
        private float _targetVolume;
        private float _duration;
        private float _elapsed;
        private bool _useUnscaledTime;
        private bool _stopOnComplete;

        /// <summary>source と同じ GameObject にフェード役を付けて返す。</summary>
        public static AudioSourceFader AddTo(AudioSource source)
        {
            var fader = source.gameObject.AddComponent<AudioSourceFader>();
            fader._source = source;
            fader.enabled = false;
            return fader;
        }

        /// <summary>
        /// 今の音量から targetVolume へ変える。進行中のフェードは打ち切って置き換える。
        /// </summary>
        /// <param name="stopOnComplete">変え終わったら AudioSource を止めるか（フェードアウト用）</param>
        /// <param name="useUnscaledTime">時間の停止（Time.timeScale = 0）の影響を受けずに進めるか</param>
        public void FadeTo(
            float targetVolume,
            float duration,
            bool stopOnComplete,
            bool useUnscaledTime
        )
        {
            _startVolume = _source.volume;
            _targetVolume = targetVolume;
            _duration = duration;
            _elapsed = 0f;
            _stopOnComplete = stopOnComplete;
            _useUnscaledTime = useUnscaledTime;

            if (duration <= 0f)
            {
                Complete();
                return;
            }
            enabled = true;
        }

        /// <summary>進行中のフェードを、その時点の音量のまま打ち切る。</summary>
        public void Cancel() => enabled = false;

        private void Update()
        {
            _elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / _duration);
            _source.volume = Mathf.Lerp(_startVolume, _targetVolume, progress);
            if (progress >= 1f)
                Complete();
        }

        private void Complete()
        {
            _source.volume = _targetVolume;
            enabled = false;
            if (_stopOnComplete)
                _source.Stop();
        }
    }
}
