using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 総合焦り度（0〜100）を、AI が判断に使う段階（平常／動揺／焦り）に変換する。
    ///
    /// 境目の前後で数値が揺れても段階が毎フレーム切り替わらないよう、
    /// 上がる時の境目と下がる時の境目をずらす（ヒステリシス）。
    /// さらに段階が下がる時だけ最低保持時間を設け、焦りが一瞬途切れただけでボスが攻めを緩めないようにする。
    /// 上がる時は待たずに反映し、追い込む機会を逃さない。
    /// </summary>
    [Serializable]
    public class PanicLevelTracker
    {
        [Tooltip("平常→動揺 に上がる焦り度")]
        [SerializeField]
        [Range(0f, 100f)]
        private float _tenseEnterScore = 30f;

        [Tooltip("動揺→平常 に下がる焦り度。上がる境目より低くする")]
        [SerializeField]
        [Range(0f, 100f)]
        private float _tenseExitScore = 20f;

        [Tooltip("動揺→焦り に上がる焦り度")]
        [SerializeField]
        [Range(0f, 100f)]
        private float _panicEnterScore = 60f;

        [Tooltip("焦り→動揺 に下がる焦り度。上がる境目より低くする")]
        [SerializeField]
        [Range(0f, 100f)]
        private float _panicExitScore = 45f;

        [Tooltip("段階が下がるまでに、今の段階を最低限保つ時間（秒）")]
        [SerializeField]
        [Min(0f)]
        private float _minHoldSeconds = 1.5f;

        private float _levelEnteredTime = float.NegativeInfinity;

        /// <summary>現在の段階。</summary>
        public PanicLevel Level { get; private set; } = PanicLevel.Calm;

        /// <summary>焦り度から段階を更新する。段階が変わったら true を返す。</summary>
        public bool Update(float score)
        {
            PanicLevel next = Evaluate(score);
            if (next == Level)
                return false;

            if (next < Level && Time.time - _levelEnteredTime < _minHoldSeconds)
                return false;

            Level = next;
            _levelEnteredTime = Time.time;
            return true;
        }

        public void Reset()
        {
            Level = PanicLevel.Calm;
            _levelEnteredTime = float.NegativeInfinity;
        }

        private PanicLevel Evaluate(float score)
        {
            switch (Level)
            {
                case PanicLevel.Panic:
                    if (score >= _panicExitScore)
                        return PanicLevel.Panic;
                    return score >= _tenseExitScore ? PanicLevel.Tense : PanicLevel.Calm;

                case PanicLevel.Tense:
                    if (score >= _panicEnterScore)
                        return PanicLevel.Panic;
                    return score >= _tenseExitScore ? PanicLevel.Tense : PanicLevel.Calm;

                default:
                    if (score >= _panicEnterScore)
                        return PanicLevel.Panic;
                    return score >= _tenseEnterScore ? PanicLevel.Tense : PanicLevel.Calm;
            }
        }
    }
}
