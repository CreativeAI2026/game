using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵に聞こえる物音の通知。出す側(足音・矢の着弾・ハンマー)と聞く側(敵AI)を直接つながずに結ぶ。
    /// 鳴らす音(BGM・効果音)は Sound の担当で、ここは敵の知覚のためだけに使う。
    /// </summary>
    public static class NoiseEventBus
    {
        /// <summary>物音がしたときに、種別・位置・届く半径を知らせる。</summary>
        public static event Action<NoiseEvent> OnNoiseEmitted;

        /// <summary>物音を出す。</summary>
        public static void Emit(NoiseEvent noise) => OnNoiseEmitted?.Invoke(noise);

        /// <summary>購読を外す。</summary>
        // Domain Reload を切った Editor で Play を繰り返したとき、前回の Play の購読を持ち越さないよう、Play 開始時に戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState() => OnNoiseEmitted = null;
    }
}
