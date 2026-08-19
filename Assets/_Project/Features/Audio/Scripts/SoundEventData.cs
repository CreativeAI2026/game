using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 音の発生イベントを表すデータ構造。
    /// 種別・発生位置・聴取半径を持ち、SoundEventBusを通じて敵AIなどのリスナーに通知される。
    /// 将来的に足音システム・矢の着弾判定などの発生源がここへ発行する。
    /// </summary>
    public class SoundEventData
    {
        /// <summary>音の種別。敵AIの反応強度や行動分岐に使用する。</summary>
        public SoundType Type;

        /// <summary>音が発生したワールド座標。敵AIが向かう目標地点となる。</summary>
        public Vector3 Position;

        /// <summary>この音が届く半径（メートル）。敵側でフィルタリングに使用する。</summary>
        public float Radius;

        public SoundEventData(SoundType type, Vector3 position, float radius)
        {
            Type = type;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// 音の種別定義。
    /// 拡張する場合はここに値を追加するだけでよい。
    /// </summary>
    public enum SoundType
    {
        /// <summary>歩行音（小）</summary>
        Walk,

        /// <summary>走り音（大）</summary>
        Run,

        /// <summary>弓が壁に刺さった音（中）</summary>
        ArrowHit,
    }
}
