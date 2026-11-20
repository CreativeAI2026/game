using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の防御（腕の鎌で受け止める）と、はじき返し（連打されたら振り払う）の調整値。
    /// 防御に入るきっかけは3つ: プレイヤーの攻撃入力への反応・様子見に入った時の読み・攻撃の後。
    /// </summary>
    [Serializable]
    public sealed class TBossGuardSettings
    {
        [Header("反応の防御（様子見中にプレイヤーが攻撃ボタンを押した瞬間）")]
        [Tooltip("攻撃ボタンが押された瞬間に防御する確率（0〜1）")]
        [SerializeField, Range(0f, 1f)]
        private float _reactiveGuardChance = 0.3f;

        [Tooltip("反応の防御を続ける時間（秒）。終わったら様子見へ戻る")]
        [SerializeField]
        private float _reactiveGuardDuration = 1.5f;

        [Header("読みの防御（追跡などから様子見に入った時）")]
        [Tooltip(
            "様子見に入った時に、すぐ殴ってくると読んで防御する確率（0〜1）。当たれば様子見の代わりに短く防御し、終わったら攻撃する"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _readGuardChance = 0.3f;

        [Tooltip("読みの防御の長さ（秒、x:最短 y:最長）。この間だけ様子見して攻撃に移る")]
        [SerializeField]
        private Vector2 _readGuardDurationRange = new Vector2(0.5f, 1f);

        [Header("攻撃後の防御（攻撃が終わって様子見に入った時）")]
        [Tooltip(
            "攻撃の後に防御する確率（0〜1）を、HPの段階（EnemyHpPhaseTracker）ごとに並べる。HPが減るほど上げて、攻撃後の隙をつぶす"
        )]
        [SerializeField, Range(0f, 1f)]
        private float[] _postAttackGuardChanceByPhase = { 0.2f, 0.45f, 0.7f };

        [Tooltip("攻撃後の防御を続ける時間（秒）。終わったら様子見へ戻る")]
        [SerializeField]
        private float _postAttackGuardDuration = 1.5f;

        [Header("防御の向き")]
        [Tooltip(
            "正面から左右に何度までの攻撃を防ぐか。これより横・背後から当たった攻撃は通る（回り込めば崩せる）"
        )]
        [SerializeField, Range(0f, 180f)]
        private float _guardHalfAngle = 90f;

        [Header("はじき返し（防御中・被弾中に連打されたら）")]
        [Tooltip("この秒数の間に")]
        [SerializeField]
        private float _repelWindow = 2f;

        [Tooltip("この回数以上殴られたら、はじき返す。ハンマーの範囲攻撃は数えない")]
        [SerializeField, Min(1)]
        private int _repelHitCount = 4;

        [Tooltip(
            "はじき返しが届く距離（m、ボスの中心からプレイヤーの中心まで）。これより遠いプレイヤー（弓など）は振り払う動きだけで怯まない"
        )]
        [SerializeField]
        private float _repelRadius = 5f;

        [Tooltip(
            "はじき返しのモーションのどこで振り払いが当たるか（Repel ステートの正規化時間 0〜1）。クリップの振りに合わせる"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _repelImpactNormalizedTime = 0.15f;

        [Tooltip(
            "はじき返しのモーションのどこで次の攻撃（通常攻撃2か掴み）に移るか（正規化時間 0〜1）。モーションの終わりを待たずに流す"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _repelFollowUpNormalizedTime = 0.56f;

        [Tooltip("はじき返しでプレイヤーにダメージを与えるか。オフなら押し出して怯ませるだけ")]
        [SerializeField]
        private bool _repelDealsDamage;

        [Tooltip("はじき返しのダメージの攻撃力倍率。実ダメージ = 敵攻撃力 × 倍率")]
        [SerializeField]
        private float _repelDamageMultiplier = 0.5f;

        /// <summary>攻撃ボタンが押された瞬間に防御する確率。</summary>
        public float ReactiveGuardChance => _reactiveGuardChance;

        /// <summary>反応の防御を続ける時間（秒）。</summary>
        public float ReactiveGuardDuration => _reactiveGuardDuration;

        /// <summary>様子見に入った時に読みで防御する確率。</summary>
        public float ReadGuardChance => _readGuardChance;

        /// <summary>読みの防御の長さ（秒、x:最短 y:最長）。</summary>
        public Vector2 ReadGuardDurationRange => _readGuardDurationRange;

        /// <summary>攻撃後の防御を続ける時間（秒）。</summary>
        public float PostAttackGuardDuration => _postAttackGuardDuration;

        /// <summary>正面から左右に何度までの攻撃を防ぐか。</summary>
        public float GuardHalfAngle => _guardHalfAngle;

        /// <summary>連打を数える時間の幅（秒）。</summary>
        public float RepelWindow => _repelWindow;

        /// <summary>はじき返しに必要な被弾回数。</summary>
        public int RepelHitCount => _repelHitCount;

        /// <summary>はじき返しが届く距離（m）。</summary>
        public float RepelRadius => _repelRadius;

        /// <summary>はじき返しの振り払いが当たる位置（Repel ステートの正規化時間）。</summary>
        public float RepelImpactNormalizedTime => _repelImpactNormalizedTime;

        /// <summary>はじき返しから次の攻撃へ移る位置（Repel ステートの正規化時間）。</summary>
        public float RepelFollowUpNormalizedTime => _repelFollowUpNormalizedTime;

        /// <summary>はじき返しでダメージを与えるか。</summary>
        public bool RepelDealsDamage => _repelDealsDamage;

        /// <summary>はじき返しのダメージの攻撃力倍率。</summary>
        public float RepelDamageMultiplier => _repelDamageMultiplier;

        /// <summary>段階ごとに並べた攻撃後の防御確率の数。HPの段階の数と合わせる。</summary>
        public int PostAttackGuardPhaseCount => _postAttackGuardChanceByPhase.Length;

        /// <summary>HPの段階に応じた、攻撃後に防御する確率。</summary>
        public float GetPostAttackGuardChance(int phaseIndex) =>
            _postAttackGuardChanceByPhase[phaseIndex];
    }
}
