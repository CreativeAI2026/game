using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵種別のバリエーションを定義するための具象クラス。
    /// 固有の索敵ロジックやインスペクター公開パラメータを管理し、AIの個性を決定づける。
    /// </summary>
    public class TestEnemyController : EnemyBaseController
    {
        [Header("移動設定")]
        [SerializeField]
        private float chaseSpeed = 5f;

        [SerializeField]
        private float strafeSpeed = 3f;

        [SerializeField]
        private float approachSpeed = 4f;

        [SerializeField]
        private float backStepSpeed = 2f;

        [Header("距離設定")]
        [SerializeField]
        private float strafeRange = 6f;

        [SerializeField]
        private float backStepRange = 2f;

        [SerializeField]
        private float attackRange = 1.5f;

        [Header("タイマー設定")]
        [SerializeField]
        private float strafeDuration = 3f;

        [SerializeField]
        private float backStepDuration = 1.2f;

        [Header("確率設定")]
        [SerializeField]
        [Range(0f, 1f)]
        private float backStepChance = 0.5f;

        [Header("針攻撃設定")]
        [SerializeField]
        public GameObject needlePrefab;

        [SerializeField]
        private float needleAttackDistance = 10f;

        [SerializeField]
        private float needleAttackCooldown = 15f;

        [SerializeField]
        private int needleCount = 5;

        [SerializeField]
        private int needleDamage = 50;

        public float NeedleAttackDistance => needleAttackDistance;
        public float NeedleAttackCooldown => needleAttackCooldown;
        public int NeedleCount => needleCount;
        public int NeedleDamage => needleDamage;

        [HideInInspector]
        public float NeedleAttackTimer;

        [Header("検知設定")]
        [SerializeField]
        private float viewDistance = 10f;

        [SerializeField]
        private float viewAngle = 90f;

        [SerializeField]
        private LayerMask obstacleLayer;
        public LayerMask ObstacleLayer => obstacleLayer;

        public float ChaseSpeed => chaseSpeed;

        public float StrafeSpeed => strafeSpeed;

        public float ApproachSpeed => approachSpeed;

        public float BackStepSpeed => backStepSpeed;

        public float StrafeRange => strafeRange;

        public float BackStepRange => backStepRange;

        public float AttackRange => attackRange;

        public float StrafeDuration => strafeDuration;

        public float BackStepDuration => backStepDuration;

        public float BackStepChance => backStepChance;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            ChangeState(new TestEnemyIdleState(this));
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
            UpdateAnimatorParameters();

            if (NeedleAttackTimer > 0)
            {
                NeedleAttackTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// アニメーターのブレンドツリーがキャラクター基準の相対的な移動方向を要求するため、
        /// NavMeshAgentのワールド速度をローカル座標系に変換して適用する。
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (Animator == null || Agent == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(Agent.velocity);
            Animator.SetFloat("VelocityX", localVelocity.x);
            Animator.SetFloat("VelocityZ", localVelocity.z);
        }

        /// <summary>
        /// 壁越しの透視や背後の不自然な感知を防ぎ、プレイヤーのステルス行動を成立させるための視界判定。
        /// </summary>
        public bool CheckInSight()
        {
            if (Player == null)
            {
                return false;
            }

            float distanceToPlayer = Vector3.Distance(
                transform.position,
                Player.transform.position
            );
            if (distanceToPlayer > viewDistance)
            {
                return false;
            }

            Vector3 directionToPlayer = (Player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle > viewAngle)
            {
                return false;
            }

            // 地面付近から撃つと地形に遮られるため、キャラクターの胸の高さ（約1m）からレイを飛ばす
            Vector3 rayStart = transform.position + Vector3.up * 1f;
            Vector3 rayTarget = Player.transform.position + Vector3.up * 1f;
            Vector3 rayDirection = rayTarget - rayStart;

            if (
                Physics.Raycast(
                    rayStart,
                    rayDirection,
                    out RaycastHit hit,
                    distanceToPlayer,
                    obstacleLayer
                )
            )
            {
                return false;
            }

            return true;
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();

            ChangeState(new TestEnemyFlinchState(this));
        }

        public override void ForceAlert()
        {
            base.ForceAlert();

            // 既に発見済みの場合はステート遷移しない（現在の行動を中断させないため）
            if (!IsAlerted)
            {
                IsAlerted = true;
                Debug.Log("攻撃を受けた！ 発見状態になります。");
                ChangeState(new TestEnemyChaseState(this));
            }
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            ChangeState(new TestEnemyDeathState(this));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;

            Vector3 leftBoundary =
                Quaternion.AngleAxis(-viewAngle, transform.up) * transform.forward;
            Vector3 rightBoundary =
                Quaternion.AngleAxis(viewAngle, transform.up) * transform.forward;

            Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewDistance);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewDistance);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
            UnityEditor.Handles.DrawSolidArc(
                transform.position,
                transform.up,
                leftBoundary,
                viewAngle * 2f,
                viewDistance
            );
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireArc(
                transform.position,
                transform.up,
                leftBoundary,
                viewAngle * 2f,
                viewDistance
            );
#endif
        }
    }
}
