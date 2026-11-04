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

        [Tooltip("攻撃の合間に周回する時間（秒）。この範囲からランダムに決める")]
        [SerializeField]
        private Vector2 _orbitDurationRange = new Vector2(2f, 4f);

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

        [Tooltip("突きのクリップを始めてから周回に戻るまで（秒）。クリップの長さに合わせる")]
        [SerializeField]
        private float _stabRecoverDuration = 1.4f;

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

        [Tooltip("ドリルのクリップを始めてから周回に戻るまで（秒）。クリップの長さに合わせる")]
        [SerializeField]
        private float _drillRecoverDuration = 1.4f;

        [Header("わっか投げ")]
        [Tooltip("一度使ってから次に使えるまで（秒）")]
        [SerializeField]
        private float _ringCooldown = 15f;

        [Tooltip("リングのクリップを始めてから周回に戻るまで（秒）。クリップの長さに合わせる")]
        [SerializeField]
        private float _ringRecoverDuration = 1.4f;

        [Header("怯み")]
        [Tooltip("怯んで止まっている時間（秒）。怯みのクリップが来たら長さに合わせる")]
        [SerializeField]
        private float _flinchDuration = 1.0f;

        private float _nextTentacleTime;
        private float _nextRingTime;

        public KurageOrbitMover Mover { get; private set; }
        public KurageBossSounds Sounds { get; private set; }
        public KurageHaloRing Halo { get; private set; }
        public KurageRingRestraint Restraint { get; private set; }

        public float OrbitSpeed => _orbitSpeed;
        public Vector2 OrbitDurationRange => _orbitDurationRange;
        public float OrbitTowardPlayerChance => _orbitTowardPlayerChance;
        public float TurnSpeed => _turnSpeed;
        public float StabChaseDuration => _stabChaseDuration;
        public float StabChaseSpeed => _stabChaseSpeed;
        public float StabWindupDuration => _stabWindupDuration;
        public float StabRecoverDuration => _stabRecoverDuration;
        public EnemyMeleeHitbox StabHitbox => _stabHitbox;
        public float StabTurnSpeed => _stabTurnSpeed;
        public float StabExtendDuration => _stabExtendDuration;
        public float StabRetractDuration => _stabRetractDuration;
        public KurageStabReach StabReach { get; private set; }
        public KurageGroundTentacle GroundTentaclePrefab => _groundTentaclePrefab;
        public float DrillRecoverDuration => _drillRecoverDuration;
        public float RingRecoverDuration => _ringRecoverDuration;
        public float FlinchDuration => _flinchDuration;

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

        /// <summary>周回を終えた時に呼ぶ。使える攻撃の中から重みで選んで始める。</summary>
        public void StartNextAttack()
        {
            bool canTentacle = Time.time >= _nextTentacleTime;
            bool canRing = Time.time >= _nextRingTime && Halo.IsOnHead;

            float total =
                _stabWeight + (canTentacle ? _tentacleWeight : 0f) + (canRing ? _ringWeight : 0f);
            float pick = Random.value * total;

            if (canTentacle && (pick -= _tentacleWeight) < 0f)
            {
                _nextTentacleTime = Time.time + _tentacleCooldown;
                ChangeState(new KurageTentacleSummonState(this));
                return;
            }
            if (canRing && (pick -= _ringWeight) < 0f)
            {
                _nextRingTime = Time.time + _ringCooldown;
                ChangeState(new KurageRingThrowState(this));
                return;
            }
            ChangeState(new KurageStabState(this));
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
