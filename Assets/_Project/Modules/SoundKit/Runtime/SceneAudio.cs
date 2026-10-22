using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// このシーンで流す BGM と場所全体の環境音を決める。シーンに1つ置く。
    /// 空欄にした方は止める（前のシーンの音を持ち込まない）。
    /// 重ねて鳴らす曲（Overlay）は場面限りなので、ここで必ず止める。
    /// </summary>
    [AddComponentMenu("SoundKit/Scene Audio")]
    public class SceneAudio : MonoBehaviour
    {
        [Tooltip("このシーンの BGM（音楽）。空ならBGMを止める")]
        [SerializeField]
        private SoundCue _bgm;

        [Tooltip("このシーンの場所全体の環境音。空なら環境音を止める")]
        [SerializeField]
        private SoundCue _ambience;

        // Awake で行う。Overlay を鳴らす側は Start 以降に鳴らす想定のため、それを止めてしまわない
        private void Awake()
        {
            BgmPlayer.Stop(BgmLayer.Overlay);

            if (_bgm != null)
                BgmPlayer.Play(_bgm);
            else
                BgmPlayer.Stop();

            if (_ambience != null)
                AmbiencePlayer.Play(_ambience);
            else
                AmbiencePlayer.Stop();
        }
    }
}
