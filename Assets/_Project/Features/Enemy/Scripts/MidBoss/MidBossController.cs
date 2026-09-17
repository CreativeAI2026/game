using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 中ボス専用のAIコントローラ。
    /// 常に発見状態で行動し、ステートマシンで各行動を制御する。
    /// アニメーションは未実装のため、Transform直接操作で表現する。
    /// </summary>
    public class MidBossController : EnemyBaseController
    {
        [Header("移動設定")]
        [Tooltip("追跡ステートの移動速度。")]
        [SerializeField]
        private float chaseSpeed = 3f;

        [Tooltip("追跡の1ステップの移動時間（秒）。")]
        [SerializeField]
        private float chaseStepMoveDuration = 0.8f;

        [Tooltip("追跡の1ステップの停止時間（秒）。")]
        [SerializeField]
        private float chaseStepStopDuration = 0.4f;

        [Tooltip("突進速度。")]
        [SerializeField]
        private float dashSpeed = 15f;

        [Tooltip("ランダム移動の最小距離。")]
        [SerializeField]
        private float randomMoveMinDist = 3f;

        [Tooltip("ランダム移動の最大距離。")]
        [SerializeField]
        private float randomMoveMaxDist = 8f;

        [Tooltip("ジグザグ移動の最大ブレ角度（度）。")]
        [SerializeField]
        private float randomMoveZigzagAngle = 25f;

        [Tooltip("大ステップ移動の速度（追跡より速め）。")]
        [SerializeField]
        private float randomMoveStepSpeed = 6f;

        [Tooltip("大ステップ移動の回数（1〜2程度）。")]
        [SerializeField]
        private int randomMoveStepCount = 2;

        [Tooltip("大ステップ移動の1回の移動時間（秒）。")]
        [SerializeField]
        private float randomMoveStepMoveDuration = 0.6f;

        [Tooltip("大ステップ移動の1回の停止時間（秒）。追跡より短め。")]
        [SerializeField]
        private float randomMoveStepStopDuration = 0.15f;

        [Header("距離設定")]
        [Tooltip("近接攻撃可能距離。")]
        [SerializeField]
        private float attackRange = 2.5f;

        [Tooltip("遠距離攻撃を使う最大距離（近接範囲外から遠距離範囲内）。")]
        [SerializeField]
        private float rangedAttackRange = 12f;

        [Header("タイマー設定")]
        [Tooltip("攻撃後の待機時間（秒）。")]
        [SerializeField]
        private float waitAfterAttackDuration = 1.5f;

        [Tooltip("通常攻撃1の振りかぶり時間（秒）。")]
        [SerializeField]
        private float normalAttack1WindupDuration = 0.8f;

        [Tooltip("通常攻撃1のたたきつけ時間（秒）。")]
        [SerializeField]
        private float normalAttack1StrikeDuration = 0.3f;

        [Tooltip("突進予備動作時間（秒）。")]
        [SerializeField]
        private float dashWindupDuration = 1.0f;

        [Tooltip("突進の最大継続時間（秒）。壁にぶつかれば早期終了する。")]
        [SerializeField]
        private float dashMaxDuration = 3.0f;

        [Tooltip("特殊攻撃（手下生成）の演出時間（秒）。")]
        [SerializeField]
        private float specialSpawnDuration = 1.5f;

        [Tooltip("液体攻撃の予備動作時間（秒）。")]
        [SerializeField]
        private float liquidWindupDuration = 1.0f;

        [Header("障害物設定")]
        [Tooltip("突進でぶつかる壁のLayerMask。")]
        [SerializeField]
        private LayerMask obstacleLayer;

        [Header("追跡タイマー設定")]
        [Tooltip("追跡のじりじりステップ数の最小値。")]
        [SerializeField]
        private int chaseStepCountMin = 2;

        [Tooltip("追跡のじりじりステップ数の最大値。")]
        [SerializeField]
        private int chaseStepCountMax = 5;

        [Header("攻撃選択確率設定")]
        [Tooltip("Startステートで即攻撃に入る確率（0〜1）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float attackTriggerChance = 0.4f;

        [Tooltip("攻撃選択時に特殊攻撃を選ぶ確率（0〜1）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float specialAttackChance = 0.25f;

        [Tooltip("遠距離攻撃選択時に突進を選ぶ確率（その他は液体攻撃）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float dashAttackChance = 0.5f;

        [Tooltip(
            "Start→攻撃外れ時にランダム移動を選ぶ確率（その他は追跡）。近接範囲内では常にランダム移動。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float randomMoveChance = 0.5f;

        [Header("通常攻撃1設定（腕オブジェクト）")]
        [Tooltip("腕に当たるオブジェクトのTransform。")]
        [SerializeField]
        private Transform armTransform;

        [Tooltip("振りかぶり目標回転（ローカルEuler角）。アニメーション導入後は不要。")]
        [SerializeField]
        private Vector3 armWindupEuler = new Vector3(-80f, 0f, 0f);

        [Tooltip("たたきつけ目標回転（ローカルEuler角）。アニメーション導入後は不要。")]
        [SerializeField]
        private Vector3 armStrikeEuler = new Vector3(60f, 0f, 0f);

        [Tooltip("腕攻撃のダメージ量。")]
        [SerializeField]
        private float armAttackDamage = 30f;

        [Tooltip("腕のOverlapSphere半径。")]
        [SerializeField]
        private float armHitRadius = 1.2f;

        [Header("遠距離攻撃2（液体）設定")]
        [Tooltip("発射位置")]
        [SerializeField]
        private Transform liquidFirePos;

        [Tooltip("液体弾プレハブ。MidBossLiquidProjectileを持つ球オブジェクト。")]
        [SerializeField]
        private GameObject liquidPrefab;

        [Tooltip("着弾ダメージエリアプレハブ。MidBossDamageAreaを持つオブジェクト。")]
        [SerializeField]
        private GameObject damageAreaPrefab;

        [Tooltip("発射数最小値。")]
        [SerializeField]
        private int liquidShotMinCount = 3;

        [Tooltip("発射数最大値。")]
        [SerializeField]
        private int liquidShotMaxCount = 7;

        [Tooltip("扇形の全体角度（度）。")]
        [SerializeField]
        private float liquidFanAngle = 120f;

        [Tooltip("液体弾のダメージ。")]
        [SerializeField]
        private float liquidDamage = 100f;

        [Tooltip("ダメージエリア持続時間（秒）。")]
        [SerializeField]
        private float damageAreaDuration = 4f;

        [Tooltip("毒付与エリアの判定間隔（秒）。")]
        [SerializeField]
        private float damageAreaInterval = 1.0f;

        [Header("液体攻撃 毒パラメータ")]
        [Tooltip(
            "毒の持続時間（秒）。毒付与エリア内に居る間はタイマーがリセットされるため、実質的にエリア内殷在中は続く。"
        )]
        [SerializeField]
        private float poisonDuration = 5f;

        [Tooltip("毒の1tickあたりのダメージ量。")]
        [SerializeField]
        private float poisonDamagePerTick = 5f;

        [Tooltip("毒のダメージ間隔（秒）。")]
        [SerializeField]
        private float poisonTickInterval = 1.0f;

        [Header("特殊攻撃（手下生成）設定")]
        [Tooltip("手下プレハブ。MidBossMinionProjectileを持つオブジェクト。")]
        [SerializeField]
        private GameObject minionPrefab;

        [Tooltip("生成数。")]
        [SerializeField]
        private int minionCount = 5;

        [Tooltip("手下のダメージ。")]
        [SerializeField]
        private int minionDamage = 20;

        //  外部参照用プロパティ（ステートから読む）
        public float ChaseSpeed => chaseSpeed;
        public float ChaseStepMoveDuration => chaseStepMoveDuration;
        public float ChaseStepStopDuration => chaseStepStopDuration;
        public float DashSpeed => dashSpeed;
        public float RandomMoveMinDist => randomMoveMinDist;
        public float RandomMoveMaxDist => randomMoveMaxDist;
        public float RandomMoveZigzagAngle => randomMoveZigzagAngle;
        public float RandomMoveStepSpeed => randomMoveStepSpeed;
        public int RandomMoveStepCount => randomMoveStepCount;
        public float RandomMoveStepMoveDuration => randomMoveStepMoveDuration;
        public float RandomMoveStepStopDuration => randomMoveStepStopDuration;

        public float AttackRange => attackRange;
        public float RangedAttackRange => rangedAttackRange;

        public float WaitAfterAttackDuration => waitAfterAttackDuration;
        public float NormalAttack1WindupDuration => normalAttack1WindupDuration;
        public float NormalAttack1StrikeDuration => normalAttack1StrikeDuration;
        public float DashWindupDuration => dashWindupDuration;
        public float DashMaxDuration => dashMaxDuration;
        public float SpecialSpawnDuration => specialSpawnDuration;
        public float LiquidWindupDuration => liquidWindupDuration;

        public LayerMask ObstacleLayer => obstacleLayer;

        public int ChaseStepCountMin => chaseStepCountMin;
        public int ChaseStepCountMax => chaseStepCountMax;

        public float AttackTriggerChance => attackTriggerChance;
        public float SpecialAttackChance => specialAttackChance;
        public float DashAttackChance => dashAttackChance;
        public float RandomMoveChance => randomMoveChance;

        public Transform ArmTransform => armTransform;
        public Vector3 ArmWindupEuler => armWindupEuler;
        public Vector3 ArmStrikeEuler => armStrikeEuler;
        public float ArmAttackDamage => armAttackDamage;
        public float ArmHitRadius => armHitRadius;

        public Transform LiquidFirePos => liquidFirePos;
        public GameObject LiquidPrefab => liquidPrefab;
        public GameObject DamageAreaPrefab => damageAreaPrefab;
        public int LiquidShotMinCount => liquidShotMinCount;
        public int LiquidShotMaxCount => liquidShotMaxCount;
        public float LiquidFanAngle => liquidFanAngle;
        public float LiquidDamage => liquidDamage;
        public float DamageAreaDuration => damageAreaDuration;
        public float DamageAreaInterval => damageAreaInterval;
        public float PoisonDuration => poisonDuration;
        public float PoisonDamagePerTick => poisonDamagePerTick;
        public float PoisonTickInterval => poisonTickInterval;

        public GameObject MinionPrefab => minionPrefab;
        public int MinionCount => minionCount;
        public int MinionDamage => minionDamage;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            // イベント終了後の強制戦闘のため、常に発見状態で開始する
            IsAlerted = true;
            ChangeState(new MidBossStartState(this));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();
            // TODO: アニメーション導入時はここで UpdateAnimatorParameters() を呼ぶ
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();
            ChangeState(new MidBossFlinchState(this));
        }

        /// <summary>
        /// 常に発見状態なので ForceAlert は無視する。
        /// </summary>
        public override void ForceAlert()
        {
            // IsAlerted は Start() で true に設定済みのため何もしない
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            ChangeState(new MidBossDeathState(this));
        }

        //  ヘルパーメソッド（ステートから呼ぶ）

        /// <summary>
        /// プレイヤーとの距離を返す。Player が null の場合は float.MaxValue。
        /// </summary>
        public float DistanceToPlayer()
        {
            if (Player == null)
                return float.MaxValue;
            return Vector3.Distance(transform.position, Player.transform.position);
        }

        /// <summary>
        /// 距離に応じた攻撃ステートへ遷移する共通メソッド。
        /// 近接範囲内なら通常攻撃1 or 特殊攻撃、遠距離範囲内なら遠距離攻撃 or 特殊攻撃。
        /// </summary>
        public void TransitionToMeleeAttack()
        {
            if (Random.value <= specialAttackChance)
            {
                ChangeState(new MidBossSpecialAttackState(this));
            }
            else
            {
                ChangeState(new MidBossNormalAttack1State(this));
            }
        }

        /// <summary>
        /// 遠距離攻撃を選択して遷移する（突進 or 液体 or 特殊攻撃）。
        /// </summary>
        public void TransitionToRangedAttack()
        {
            if (Random.value <= specialAttackChance)
            {
                ChangeState(new MidBossSpecialAttackState(this));
            }
            else if (Random.value <= dashAttackChance)
            {
                ChangeState(new MidBossDashAttackState(this));
            }
            else
            {
                ChangeState(new MidBossLiquidAttackState(this));
            }
        }

        /// <summary>
        /// 追跡 or ランダム移動をランダムに選択して遷移する。
        /// </summary>
        public void TransitionToMoveState()
        {
            if (Random.value <= randomMoveChance)
            {
                ChangeState(new MidBossRandomMoveState(this));
            }
            else
            {
                ChangeState(new MidBossChaseState(this));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 近接攻撃範囲
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 遠距離攻撃範囲
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
        }
#endif
    }
}
