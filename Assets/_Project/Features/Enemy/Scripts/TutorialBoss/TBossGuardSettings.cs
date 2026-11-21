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

        [Header("遠距離の防御（距離を取って弓を構える・撃つ・鎌を投げた時）")]
        [Tooltip(
            "距離を取って遠距離攻撃をしてきそうな時に、構えてにじり寄る確率（0〜1）を、HPの段階ごとに並べる。外れたら普段どおり追いかける"
        )]
        [SerializeField, Range(0f, 1f)]
        private float[] _rangedGuardChanceByPhase = { 0.35f, 0.5f, 0.65f };

        [Tooltip(
            "遠距離の防御の確率を引いてから、次に引けるまでの間隔（秒）。撃つたびに引き直して必ず構えるのを防ぐ"
        )]
        [SerializeField]
        private float _rangedGuardRollInterval = 3f;

        [Tooltip(
            "構えてにじり寄る速さ（m/秒）。モーションはこの速さに合わせて再生速度が変わるので、足は滑らない"
        )]
        [SerializeField]
        private float _guardAdvanceSpeed = 0.45f;

        [Tooltip(
            "にじり寄りのモーション（TBoss_GuardAdvance）を1倍で再生した時に進む速さ（m/秒）。1周1.67秒で0.75m（ボスの大きさ2.5倍の時）。モーションを作り直した時だけ変える"
        )]
        [SerializeField]
        private float _guardAdvanceClipSpeed = 0.45f;

        [Tooltip(
            "構えてにじり寄る時間（秒、x:最短 y:最長）。この間にプレイヤーが攻撃の間合いへ入ってこなければ、自分から攻撃に入る"
        )]
        [SerializeField]
        private Vector2 _guardAdvanceDurationRange = new Vector2(6f, 9f);

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

        /// <summary>遠距離の防御の確率を引く間隔（秒）。</summary>
        public float RangedGuardRollInterval => _rangedGuardRollInterval;

        /// <summary>構えてにじり寄る速さ（m/秒）。</summary>
        public float GuardAdvanceSpeed => _guardAdvanceSpeed;

        /// <summary>にじり寄りのモーションを1倍で再生した時に進む速さ（m/秒）。</summary>
        public float GuardAdvanceClipSpeed => _guardAdvanceClipSpeed;

        /// <summary>構えてにじり寄る時間（秒、x:最短 y:最長）。</summary>
        public Vector2 GuardAdvanceDurationRange => _guardAdvanceDurationRange;

        /// <summary>段階ごとに並べた遠距離の防御確率の数。HPの段階の数と合わせる。</summary>
        public int RangedGuardPhaseCount => _rangedGuardChanceByPhase.Length;

        /// <summary>HPの段階に応じた、遠距離攻撃に対して構えてにじり寄る確率。</summary>
        public float GetRangedGuardChance(int phaseIndex) => _rangedGuardChanceByPhase[phaseIndex];

        /// <summary>正面から左右に何度までの攻撃を防ぐか。</summary>
        public float GuardHalfAngle => _guardHalfAngle;

        /// <summary>連打を数える時間の幅（秒）。</summary>
        public float RepelWindow => _repelWindow;

        /// <summary>はじき返しに必要な被弾回数。</summary>
        public int RepelHitCount => _repelHitCount;

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
