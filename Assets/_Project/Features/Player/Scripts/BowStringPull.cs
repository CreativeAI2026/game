using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓の弦の引き点(モデルの根本ボーン)を動かす。
    /// 引いている間は引き手に合わせ、離すと元の位置へ戻す。弦のメッシュは根本ボーンに付いているので、骨を動かすと弦が伸びる。
    /// 手の位置はアニメーションで決まるため、骨はアニメーションの評価後(LateUpdate)に <see cref="Apply"/> で動かす。
    /// </summary>
    public class BowStringPull
    {
        private readonly Transform _nockBone;
        private readonly Vector3 _restLocalPosition;
        private Transform _hand;

        public BowStringPull(Transform nockBone)
        {
            _nockBone = nockBone;
            _restLocalPosition = nockBone.localPosition;
        }

        /// <summary>弦の引き点(矢の尾が掛かる位置)。</summary>
        public Vector3 NockPosition => _nockBone.position;

        /// <summary>以後 <see cref="Apply"/> のたびに、弦の引き点を hand の位置へ合わせる。</summary>
        public void StartPull(Transform hand) => _hand = hand;

        /// <summary>弦を離して元の位置へ戻す。</summary>
        public void Release()
        {
            _hand = null;
            _nockBone.localPosition = _restLocalPosition;
        }

        public void Apply()
        {
            if (_hand != null)
                _nockBone.position = _hand.position;
        }
    }
}
