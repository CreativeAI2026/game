using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの、HPの段階（<see cref="EnemyHpPhaseTracker"/>）ごとの行動の値。
    /// </summary>
    [Serializable]
    public struct KuragePhaseSettings
    {
        [Tooltip("周回時間の範囲(x:最小時間、y:最大時間)")]
        [SerializeField]
        private Vector2 _orbitDurationRange;

        [Tooltip("1回の周回中にスピードアップする確率（0〜1）")]
        [SerializeField, Range(0f, 1f)]
        private float _speedUpProbability;

        [Tooltip("1回の周回中に切り返す確率（0〜1）")]
        [SerializeField, Range(0f, 1f)]
        private float _turnProbability;

        [Tooltip("2連続攻撃の確率（0〜1）")]
        [SerializeField, Range(0f, 1f)]
        private float _doubleAttackProbability;

        /// <summary>周回時間の範囲(x:最小時間、y:最大時間)。</summary>
        public Vector2 OrbitDurationRange => _orbitDurationRange;

        /// <summary>1回の周回中にスピードアップする確率。</summary>
        public float SpeedUpProbability => _speedUpProbability;

        /// <summary>1回の周回中に切り返す確率。</summary>
        public float TurnProbability => _turnProbability;

        /// <summary>2連続攻撃の確率。</summary>
        public float DoubleAttackProbability => _doubleAttackProbability;

        public KuragePhaseSettings(
            Vector2 orbitDurationRange,
            float speedUpProbability,
            float turnProbability,
            float doubleAttackProbability
        )
        {
            _orbitDurationRange = orbitDurationRange;
            _speedUpProbability = speedUpProbability;
            _turnProbability = turnProbability;
            _doubleAttackProbability = doubleAttackProbability;
        }
    }
}
