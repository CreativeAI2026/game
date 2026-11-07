using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの行動の司令塔。ステージの周りを浮いて左右に回り(<see cref="KurageOrbitMover"/>)、
    /// 触手の突き・地面からの触手・わっか投げを使い分ける。
    ///
    /// 各攻撃の中身はステート(States/)と、攻撃ごとの部品(地面の触手・わっか・拘束)が持つ。
    /// ここは攻撃の選択と、部品どうしのつなぎ(わっかが捕まえたら拘束し、ボスは待機する等)だけを行う。
    /// </summary>
    [RequireComponent(typeof(EnemyStatus))]
    [RequireComponent(typeof(KuragePhaseTracker))]
    [RequireComponent(typeof(KurageOrbitMover))]
    [RequireComponent(typeof(KurageBossSounds))]
    [RequireComponent(typeof(KurageHaloRing))]
    [RequireComponent(typeof(KurageRingRestraint))]
    [RequireComponent(typeof(KurageStabReach))]
    public class KurageBossController : EnemyBaseController
    {
        [Header("周回")]
        [Tooltip("周回の速さ（m/秒）")]
        [SerializeField]
        private float _orbitSpeed = 6f;

        [Tooltip("スピードアップ時の周回の速さ（m/秒）")]
        [SerializeField]
        private float _highOrbitSpeed = 10f;

        [Tooltip(
            "スピードアップする長さ。その周回の時間に対する割合（0〜1）。段階で周回時間が短くなっても、普通の速さと混ざるよう秒ではなく割合で決める"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _speedUpDurationRatio = 0.4f;

        [Tooltip(
            "周回の速さを変える時の加速度（m/秒²）。スピードアップや切り返しで一瞬で切り替わってカクッと見えないよう、少しずつ変える"
        )]
        [SerializeField]
        private float _orbitAcceleration = 20f;

        [Tooltip("周回する時にプレイヤーの方へ回り込む確率。外れると逆へ回る")]
        [SerializeField, Range(0f, 1f)]
        private float _orbitTowardPlayerChance = 0.7f;

        [Tooltip("正面をステージへ向け直す速さ（度/秒）。突きで向きを変えた後に戻る速さ")]
        [SerializeField]
        private float _turnSpeed = 90f;

        [Header("攻撃の選びやすさ（重み）")]
        [SerializeField]
        private float _stabWeight = 3f;

        [SerializeField]
        private float _tentacleWeight = 2f;

        [SerializeField]
        private float _ringWeight = 1.5f;

        [Tooltip(
            "直前と同じ攻撃の重みに掛ける倍率（0〜1）。同じ攻撃が続いて行動が読まれないようにする。2回続いた攻撃は3回目には選ばない"
        )]
        [SerializeField, Range(0f, 1f)]
        private float _repeatWeightScale = 0.4f;

        [Header("触手の突き")]
        [Tooltip("突く前にプレイヤーを追いかける時間（秒）")]
        [SerializeField]
        private float _stabChaseDuration = 2.5f;

        [Tooltip("追いかける速さ（m/秒）")]
        [SerializeField]
        private float _stabChaseSpeed = 10f;

        [Tooltip("立ち止まってから突くまでの溜め（秒）。プレイヤーへの予兆")]
        [SerializeField]
        private float _stabWindupDuration = 0.5f;

        [Tooltip("溜めの間に、体を狙った場所へ向ける速さ（度/秒）")]
        [SerializeField]
        private float _stabTurnSpeed = 240f;

        [Tooltip("伸び始めてから伸び切るまで（秒）")]
        [SerializeField]
        private float _stabExtendDuration = 0.1f;

        [Tooltip("戻り始めてから縮み切るまで（秒）")]
        [SerializeField]
        private float _stabRetractDuration = 0.15f;

        [Tooltip("突きの触手の先端に付けた当たり判定")]
        [SerializeField]
        private EnemyMeleeHitbox _stabHitbox;

        [Header("地面からの触手")]
        [Tooltip("地面から出す触手のプレハブ")]
        [SerializeField]
        private KurageGroundTentacle _groundTentaclePrefab;

        [Tooltip("出し方のパターン。使うたびにこの中からランダムに選ぶ")]
        [SerializeField]
        private KurageTentaclePattern[] _tentaclePatterns;

        [Tooltip("一度使ってから次に使えるまで（秒）")]
        [SerializeField]
        private float _tentacleCooldown = 8f;

        [Header("わっか投げ")]
        [Tooltip("一度使ってから次に使えるまで（秒）")]
        [SerializeField]
        private float _ringCooldown = 15f;

        private float _nextTentacleTime;
        private float _nextRingTime;

        // 直前の攻撃と、それが何回続いたか。同じ攻撃の偏りを防ぐのに使う
        private KurageAttackKind? _lastAttack;
        private int _repeatCount;

        /// <summary>今の攻撃が終わった後に、もう1回攻撃を続けるか。</summary>
        private bool _followUpPending;

        private KuragePhaseTracker _phase;

        public KurageOrbitMover Mover { get; private set; }
        public KurageBossSounds Sounds { get; private set; }
        public KurageHaloRing Halo { get; private set; }
        public KurageRingRestraint Restraint { get; private set; }

        public float OrbitSpeed => _orbitSpeed;
        public float HighOrbitSpeed => _highOrbitSpeed;
        public float OrbitAcceleration => _orbitAcceleration;
        public float SpeedUpDurationRatio => _speedUpDurationRatio;
        public float SpeedUpProbability => _phase.CurrentPhase.SpeedUpProbability;
        public Vector2 OrbitDurationRange => _phase.CurrentPhase.OrbitDurationRange;
        public float TurnProbability => _phase.CurrentPhase.TurnProbability;
        public float DoubleAttackProbability => _phase.CurrentPhase.DoubleAttackProbability;
        public float OrbitTowardPlayerChance => _orbitTowardPlayerChance;
        public float TurnSpeed => _turnSpeed;
        public float StabChaseDuration => _stabChaseDuration;
        public float StabChaseSpeed => _stabChaseSpeed;
        public float StabWindupDuration => _stabWindupDuration;
        public EnemyMeleeHitbox StabHitbox => _stabHitbox;
        public float StabTurnSpeed => _stabTurnSpeed;
        public float StabExtendDuration => _stabExtendDuration;
        public float StabRetractDuration => _stabRetractDuration;
        public KurageStabReach StabReach { get; private set; }
        public KurageGroundTentacle GroundTentaclePrefab => _groundTentaclePrefab;

        public override bool IsAttacking =>
            currentState is KurageStabState or KurageTentacleSummonState or KurageRingThrowState;

        protected override void Awake()
        {
            base.Awake();
            Assert.IsNotNull(Player, $"{name}: Player タグの付いたプレイヤーが見つかりません");
            Assert.IsNotNull(_stabHitbox, $"{name}: 突きの当たり判定が設定されていません");
            Assert.IsNotNull(
                _groundTentaclePrefab,
                $"{name}: 地面の触手のプレハブが設定されていません"
            );
            Assert.IsTrue(
                _tentaclePatterns != null && _tentaclePatterns.Length > 0,
                $"{name}: 地面の触手のパターンが1つも設定されていません"
            );

            Mover = GetComponent<KurageOrbitMover>();
            Sounds = GetComponent<KurageBossSounds>();
            Halo = GetComponent<KurageHaloRing>();
            Restraint = GetComponent<KurageRingRestraint>();
            StabReach = GetComponent<KurageStabReach>();
            _phase = GetComponent<KuragePhaseTracker>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Halo.Caught += OnHaloCaught;
            Restraint.Escaped += OnRestraintEscaped;
            Restraint.Released += OnRestraintReleased;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Halo.Caught -= OnHaloCaught;
            Restraint.Escaped -= OnRestraintEscaped;
            Restraint.Released -= OnRestraintReleased;
        }

        protected override void Start()
        {
            base.Start();
            ChangeState(new KurageOrbitState(this));
        }

        /// <summary>周回を終えた時に呼ぶ。2連続にするかを抽選し、使える攻撃の中から重みで選んで始める。</summary>
        public void StartNextAttack()
        {
            bool canTentacle = Time.time >= _nextTentacleTime;
            bool canRing = Time.time >= _nextRingTime && Halo.IsOnHead;

            _followUpPending = Random.value < DoubleAttackProbability;
            StartWeightedAttack(canTentacle, canRing);
        }

        /// <summary>
        /// 攻撃ステートが終わった時に呼ぶ。2連続が決まっていれば2回目を始め、なければ周回に戻る。
        /// 2回目の内容は、1回目の間に変わった状況(わっかの有無)を反映するためここで選ぶ。
        /// </summary>
        public void FinishAttack()
        {
            if (!_followUpPending || !AutoAttacks)
            {
                ChangeState(new KurageOrbitState(this));
                return;
            }

            _followUpPending = false;
            // 2回目はクールダウンを無視する。ただし、わっかは1つしかないので頭上にある時だけ投げられる。
            // 触手を2回続けると地面が触手で埋まって避け場所が無くなるため、触手の後に触手は出さない
            StartWeightedAttack(
                canTentacle: _lastAttack != KurageAttackKind.Tentacles,
                canRing: Halo.IsOnHead
            );
        }

        /// <summary>使える攻撃の中から重みで1つ選び、クールダウンを入れて始める。直前と同じ攻撃は選ばれにくくする。</summary>
        private void StartWeightedAttack(bool canTentacle, bool canRing)
        {
            float stab = RepeatAdjustedWeight(KurageAttackKind.Stab, _stabWeight);
            float tentacle = canTentacle
                ? RepeatAdjustedWeight(KurageAttackKind.Tentacles, _tentacleWeight)
                : 0f;
            float ring = canRing ? RepeatAdjustedWeight(KurageAttackKind.Ring, _ringWeight) : 0f;
            // 使える攻撃が続けて出した突きしか無い時は、偏りを防ぐより攻撃が止まらないことを優先する
            if (stab + tentacle + ring <= 0f)
                stab = _stabWeight;
            float pick = Random.value * (stab + tentacle + ring);

            if ((pick -= tentacle) < 0f)
            {
                _nextTentacleTime = Time.time + _tentacleCooldown;
                RecordAttack(KurageAttackKind.Tentacles);
                ChangeState(new KurageTentacleSummonState(this));
                return;
            }
            if ((pick -= ring) < 0f)
            {
                _nextRingTime = Time.time + _ringCooldown;
                // わっかは投げた後も飛び続け、捕まえるのが次の攻撃の最中になりうる。
                // その時に出した地面の触手が拘束中のプレイヤーに当たらないよう、わっかの後は続けない
                _followUpPending = false;
                RecordAttack(KurageAttackKind.Ring);
                ChangeState(new KurageRingThrowState(this));
                return;
            }
            RecordAttack(KurageAttackKind.Stab);
            ChangeState(new KurageStabState(this));
        }

        /// <summary>直前に続けて出した攻撃ほど重みを下げる。2回続いた攻撃は0にする。</summary>
        private float RepeatAdjustedWeight(KurageAttackKind kind, float weight)
        {
            if (kind != _lastAttack)
                return weight;
            return _repeatCount >= 2 ? 0f : weight * _repeatWeightScale;
        }

        private void RecordAttack(KurageAttackKind kind)
        {
            _repeatCount = kind == _lastAttack ? _repeatCount + 1 : 1;
            _lastAttack = kind;
        }

        /// <summary>周回の後に自分で攻撃を選ぶか。デバッグで攻撃を1つずつ確かめる時は止める。</summary>
        public bool AutoAttacks { get; set; } = true;

        /// <summary>ボスに並べた地面の触手の出し方。</summary>
        public IReadOnlyList<KurageTentaclePattern> TentaclePatterns => _tentaclePatterns;

        /// <summary>今のステートの名前(デバッグ表示用)。</summary>
        public string CurrentStateName => currentState != null ? currentState.GetType().Name : "-";

        /// <summary>
        /// 指定した攻撃をすぐに始める(デバッグ用)。倒れている・わっかで締めている間は始めない。
        /// わっか投げは、わっかが頭上に戻っていなければ始めない。
        /// </summary>
        /// <param name="tentaclePattern">地面の触手を出す時の出し方。null なら並べた中から選ぶ</param>
        /// <returns>始められたか</returns>
        public bool TryStartAttack(
            KurageAttackKind attack,
            KurageTentaclePattern tentaclePattern = null
        )
        {
            if (currentState is KurageDeathState or KurageIdleState)
                return false;

            // デバッグで単発の攻撃を確かめる時に、前の抽選結果で2回目が出ないようにする
            _followUpPending = false;

            switch (attack)
            {
                case KurageAttackKind.Stab:
                    ChangeState(new KurageStabState(this));
                    return true;
                case KurageAttackKind.Tentacles:
                    ChangeState(new KurageTentacleSummonState(this, tentaclePattern));
                    return true;
                case KurageAttackKind.Ring:
                    if (!Halo.IsOnHead)
                        return false;
                    ChangeState(new KurageRingThrowState(this));
                    return true;
            }
            return false;
        }

        /// <summary>地面の触手の出し方をパターンの中から1つ選ぶ。</summary>
        public KurageTentaclePattern PickTentaclePattern() =>
            _tentaclePatterns[Random.Range(0, _tentaclePatterns.Length)];

        /// <summary><see cref="KurageAnimationEventRelay"/> から呼ばれ、今のステートへ渡す。</summary>
        public void HandleAnimationEvent(KurageAnimationEvent animationEvent)
        {
            (currentState as KurageBaseState)?.OnAnimationEvent(animationEvent);
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();
            // 拘束中は攻撃を受けないはずだが、念のため待機を崩さない
            if (currentState is KurageDeathState or KurageIdleState)
                return;
            ChangeState(new KurageFlinchState(this));
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            Restraint.Cancel();
            Halo.ReturnToHead();
            ChangeState(new KurageDeathState(this));
        }

        private void OnHaloCaught(Transform player)
        {
            if (currentState is KurageDeathState)
            {
                Halo.ReturnToHead();
                return;
            }
            Restraint.Begin(player);
            // 締めている間は追撃せず待つ
            ChangeState(new KurageIdleState(this));
        }

        private void OnRestraintEscaped() => Halo.Break();

        private void OnRestraintReleased() => Halo.ReturnToHead();
    }
}
