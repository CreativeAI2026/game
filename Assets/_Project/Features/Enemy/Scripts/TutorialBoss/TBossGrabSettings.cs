using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の掴み（触手で捕まえたプレイヤーを引き寄せて電撃を流す）の調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossGrabSettings
    {
        [Tooltip("電撃ダメージ（1ティックあたり）の攻撃力倍率。実ダメージ = 敵攻撃力 × 倍率。")]
        [SerializeField]
        private float _damageMultiplier = 0.2f;

        [Tooltip("電撃ダメージを与える間隔（秒）。")]
        [SerializeField]
        private float _damageInterval = 0.5f;

        [Tooltip("脱出に必要なゲージの最大値。")]
        [SerializeField]
        private float _escapeThreshold = 20f;

        [Tooltip("移動ボタン1入力あたりのゲージ増加量。")]
        [SerializeField]
        private float _escapePerInput = 1f;

        [Tooltip("プレイヤーを引き寄せる際にかかる時間（秒）。")]
        [SerializeField]
        private float _pullDuration = 0.5f;

        [Tooltip("プレイヤーを引き寄せる目標距離（ボス正面方向）。")]
        [SerializeField]
        private float _pullDistance = 1.5f;

        [Tooltip(
            "プレイヤーを引き寄せる目標の横方向オフセット（ボスのright方向）。正値で右、負値で左にずれる。カメラ演出に応じて調整する。"
        )]
        [SerializeField]
        private float _pullLateralOffset;

        [Tooltip("触手の引っ込み演出にかかる時間（秒）。捕獲脱出後のリトラクト。")]
        [SerializeField]
        private float _retractDuration = 0.4f;

        /// <summary>電撃ダメージ（1ティックあたり）の攻撃力倍率。</summary>
        public float DamageMultiplier => _damageMultiplier;

        /// <summary>電撃ダメージを与える間隔（秒）。</summary>
        public float DamageInterval => _damageInterval;

        /// <summary>脱出に必要なゲージの最大値。</summary>
        public float EscapeThreshold => _escapeThreshold;

        /// <summary>移動ボタン1入力あたりのゲージ増加量。</summary>
        public float EscapePerInput => _escapePerInput;

        /// <summary>プレイヤーを引き寄せる際にかかる時間（秒）。</summary>
        public float PullDuration => _pullDuration;

        /// <summary>プレイヤーを引き寄せる目標距離（ボス正面方向）。</summary>
        public float PullDistance => _pullDistance;

        /// <summary>プレイヤーを引き寄せる目標の横方向オフセット（ボスのright方向）。</summary>
        public float PullLateralOffset => _pullLateralOffset;

        /// <summary>触手の引っ込み演出にかかる時間（秒）。</summary>
        public float RetractDuration => _retractDuration;
    }
}
