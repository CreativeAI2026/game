using UnityEngine;

namespace CreativeAI.Gameplay
{
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

        [Header("検知設定")]
        [SerializeField]
        private float viewDistance = 10f;

        [SerializeField]
        private float viewAngle = 90f;

        [SerializeField]
        private LayerMask obstacleLayer;

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

        protected override void Update()
        {
            base.Update();
            UpdateAnimatorParameters();
        }

        private void UpdateAnimatorParameters()
        {
            if (Animator == null || Agent == null)
            {
                return;
            }

            // エージェントの速度をローカル座標に変換して Animator に渡す
            Vector3 localVelocity = transform.InverseTransformDirection(Agent.velocity);
            Animator.SetFloat("VelocityX", localVelocity.x);
            Animator.SetFloat("VelocityZ", localVelocity.z);
        }

        // プレイヤーの発見ロジック。インスペクターで設定した視界内に入るとtrueを返す
        public bool CheckInSight()
        {
            if (Player == null)
            {
                return false;
            }

            // 距離のチェック
            float distanceToPlayer = Vector3.Distance(
                transform.position,
                Player.transform.position
            );
            if (distanceToPlayer > viewDistance)
            {
                return false;
            }

            // 角度（視野角）のチェック
            Vector3 directionToPlayer = (Player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle > viewAngle)
            {
                return false; // 視野角より外なら見えない
            }

            // Raycastによる遮蔽物（壁）のチェック
            // お互いの足元ではなく、カプセルの中心（高さ1m付近）からレイを飛ばす
            Vector3 rayStart = transform.position + Vector3.up * 1f;
            Vector3 rayTarget = Player.transform.position + Vector3.up * 1f;
            Vector3 rayDirection = rayTarget - rayStart;

            // プレイヤーまでの距離を上限にしてRaycastを飛ばす
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
                // もしプレイヤーに届く前に「Obstacleレイヤー」の壁に当たったら、隠れていると判断
                return false;
            }

            // 壁に当たらずに視界が通っていれば発見！
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

            // 未発見状態（Idle）のときに攻撃を受けたら追跡ステートへ
            if (!IsAlerted)
            {
                IsAlerted = true;
                Debug.Log("攻撃を受けた！ 発見状態になります。");
                ChangeState(new TestEnemyChaseState(this));
            }
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
