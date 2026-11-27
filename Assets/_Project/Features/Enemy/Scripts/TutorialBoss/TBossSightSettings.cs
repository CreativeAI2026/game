using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss がプレイヤーを捕捉し続ける・見失うまでの調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossSightSettings
    {
        [Tooltip("プレイヤーが光から外れてから見失うまでの時間（秒）。")]
        [SerializeField]
        private float _lostSightDuration = 3f;

        [Tooltip(
            "交戦中（追跡・様子見）は、この水平距離（m）以内で遮蔽物が無ければ光の外でもプレイヤーを捕捉し続ける。\n"
                + "光円錐は細いため、0にすると至近距離で横に回り込まれただけで見失う。"
        )]
        [SerializeField]
        private float _combatCloseSenseRadius = 6f;

        /// <summary>プレイヤーが光から外れてから見失うまでの時間（秒）。焦りへの追い込みの補正前。</summary>
        public float LostSightDuration => _lostSightDuration;

        /// <summary>交戦中、光の外でもプレイヤーを捕捉し続ける水平距離（m）。</summary>
        public float CombatCloseSenseRadius => _combatCloseSenseRadius;
    }
}
