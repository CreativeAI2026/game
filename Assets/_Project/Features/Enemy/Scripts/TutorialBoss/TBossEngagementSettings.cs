using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の交戦中（追跡・様子見）の間合いと、攻撃を仕掛ける間隔の調整値。
    /// </summary>
    [Serializable]
    public sealed class TBossEngagementSettings
    {
        [Tooltip(
            "様子見で保つ間合いの内側（m、中心間の距離）。これより近いと下がる。体格が大きいので体同士が触れない程度に取る。"
        )]
        [SerializeField]
        private float _watchDistanceMin = 6f;

        [Tooltip("様子見で保つ間合いの外側（m、中心間の距離）。これより遠いと詰める。")]
        [SerializeField]
        private float _watchDistanceMax = 9f;

        [Tooltip(
            "通常攻撃・鎌は、この距離以内なら走り込んでから振る。様子見の間合いから直接仕掛けられるよう、様子見の外側より長くする。"
        )]
        [SerializeField]
        private float _meleeChargeRange = 10f;

        [Tooltip(
            "攻撃と攻撃の間隔の基準（秒）。実際は0.6〜1.4倍でばらつく。様子見・追跡をまたいで数えるので、離れて仕切り直されても延びない。"
        )]
        [SerializeField]
        private float _attackInterval = 2f;

        [Tooltip(
            "追跡で間合いに入った時、様子見を挟まずそのまま攻撃に移る確率（0〜1）。毎回様子見から入る一定パターンを崩す。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float _directAssaultChance = 0.35f;

        /// <summary>様子見で保つ間合いの内側（m、中心間の距離）。</summary>
        public float WatchDistanceMin => _watchDistanceMin;

        /// <summary>様子見で保つ間合いの外側（m、中心間の距離）。</summary>
        public float WatchDistanceMax => _watchDistanceMax;

        /// <summary>通常攻撃・鎌を走り込んでから振れる距離（m）。</summary>
        public float MeleeChargeRange => _meleeChargeRange;

        /// <summary>攻撃と攻撃の間隔の基準（秒）。焦りへの追い込みの補正前。</summary>
        public float AttackInterval => _attackInterval;

        /// <summary>様子見を挟まず攻撃に移る確率。焦りへの追い込みの補正前。</summary>
        public float DirectAssaultChance => _directAssaultChance;
    }
}
