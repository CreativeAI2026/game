using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタの試聴。ファイルに書き出さず、波形データからその場で AudioClip を作って鳴らす。
    /// 鳴らす AudioSource はシーンに保存されず Hierarchy にも出ない。ミキサーは通さない（素材そのものの音を聞くため）。
    /// </summary>
    public class AudioPreviewPlayer
    {
        private AudioSource _source;
        private AudioClip _clip;

        public bool IsPlaying => _source != null && _source.isPlaying;

        /// <summary>再生中の位置（秒）。</summary>
        public double PlaybackSeconds =>
            IsPlaying ? _source.timeSamples / (double)_clip.frequency : 0;

        public void Play(float[] samples, int channels, int sampleRate)
        {
            Stop();
            int frameCount = samples.Length / channels;
            if (frameCount == 0)
                return;

            ReleaseClip();
            _clip = AudioClip.Create("AudioEditPreview", frameCount, channels, sampleRate, false);
            _clip.SetData(samples, 0);

            AudioSource source = GetSource();
            source.clip = _clip;
            source.Play();
        }

        public void Stop()
        {
            if (_source != null)
                _source.Stop();
        }

        /// <summary>ウィンドウを閉じるときに、試聴用に作ったものを片付ける。</summary>
        public void Dispose()
        {
            if (_source != null)
                Object.DestroyImmediate(_source.gameObject);
            ReleaseClip();
        }

        private void ReleaseClip()
        {
            if (_clip != null)
                Object.DestroyImmediate(_clip);
            _clip = null;
        }

        private AudioSource GetSource()
        {
            if (_source != null)
                return _source;

            var go = EditorUtility.CreateGameObjectWithHideFlags(
                "AudioEditPreview",
                HideFlags.HideAndDontSave,
                typeof(AudioSource)
            );
            _source = go.GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            // ゲーム中のポーズ（AudioListener.pause）の最中でも試聴できるように
            _source.ignoreListenerPause = true;
            return _source;
        }
    }
}
