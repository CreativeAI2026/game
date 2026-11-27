using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの触手の突き（追いかけて、溜めて、伸ばして戻す）の調整値。
    /// </summary>
    [Serializable]
    public sealed class KurageStabSettings
    {
        [Tooltip("突く前にプレイヤーを追いかける時間（秒）")]
        [SerializeField]
        private float _chaseDuration = 2.5f;

        [Tooltip("追いかける速さ（m/秒）")]
        [SerializeField]
        private float _chaseSpeed = 10f;

        [Tooltip("立ち止まってから突くまでの溜め（秒）。プレイヤーへの予兆")]
        [SerializeField]
        private float _windupDuration = 0.5f;

        [Tooltip("溜めの間に、体を狙った場所へ向ける速さ（度/秒）")]
        [SerializeField]
        private float _turnSpeed = 240f;

        [Tooltip("伸び始めてから伸び切るまで（秒）")]
        [SerializeField]
        private float _extendDuration = 0.1f;

        [Tooltip("戻り始めてから縮み切るまで（秒）")]
        [SerializeField]
        private float _retractDuration = 0.15f;

        /// <summary>突く前にプレイヤーを追いかける時間（秒）。</summary>
        public float ChaseDuration => _chaseDuration;

        /// <summary>追いかける速さ（m/秒）。</summary>
        public float ChaseSpeed => _chaseSpeed;

        /// <summary>立ち止まってから突くまでの溜め（秒）。</summary>
        public float WindupDuration => _windupDuration;

        /// <summary>溜めの間に体を狙った場所へ向ける速さ（度/秒）。</summary>
        public float TurnSpeed => _turnSpeed;

        /// <summary>伸び始めてから伸び切るまで（秒）。</summary>
        public float ExtendDuration => _extendDuration;

        /// <summary>戻り始めてから縮み切るまで（秒）。</summary>
        public float RetractDuration => _retractDuration;
    }
}
