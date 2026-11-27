using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の移動の速さの調整値。歩き（未発見）・走り（発見・音への反応）・様子見の横移動。
    /// </summary>
    [Serializable]
    public sealed class TBossMovementSettings
    {
        [Tooltip("未発見時の歩行速度。")]
        [SerializeField]
        private float _walkSpeed = 1.5f;

        [Tooltip("発見時・音反応時の走り速度。")]
        [SerializeField]
        private float _runSpeed = 5f;

        [Tooltip("発見時様子見移動の速度")]
        [SerializeField]
        private float _strafeSpeed = 2f;

        /// <summary>未発見時の歩行速度。</summary>
        public float WalkSpeed => _walkSpeed;

        /// <summary>発見時・音反応時の走り速度。</summary>
        public float RunSpeed => _runSpeed;

        /// <summary>様子見で横に回り込む速度。</summary>
        public float StrafeSpeed => _strafeSpeed;
    }
}
