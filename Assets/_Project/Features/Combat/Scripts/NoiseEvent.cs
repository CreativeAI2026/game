using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵に聞こえる物音1つ分(種別・位置・届く半径)。<see cref="NoiseEventBus"/> で敵AIに知らせる。
    /// 足音のたびに出るので、メモリを確保しない struct にしている。
    /// </summary>
    public readonly struct NoiseEvent
    {
        /// <summary>音の種別。敵AIの反応強度や行動分岐に使用する。</summary>
        public readonly NoiseType Type;

        /// <summary>音が発生したワールド座標。敵AIが向かう目標地点となる。</summary>
        public readonly Vector3 Position;

        /// <summary>この音が届く半径（メートル）。敵側でフィルタリングに使用する。</summary>
        public readonly float Radius;

        public NoiseEvent(NoiseType type, Vector3 position, float radius)
        {
            Type = type;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>物音の種別。敵AIの反応の強さや行動の分け方に使う。</summary>
    public enum NoiseType
    {
        /// <summary>歩行音（小）</summary>
        Walk,

        /// <summary>走り音（大）</summary>
        Run,

        /// <summary>弓が壁に刺さった音（中）</summary>
        ArrowHit,

        /// <summary>ハンマーの叩きつけ(範囲攻撃)の噴射音（大）</summary>
        HammerThrust,

        /// <summary>ハンマーの叩きつけ(範囲攻撃)で地面を叩いた音（大）</summary>
        HammerSlam,
    }
}
