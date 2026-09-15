using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを発見した後、攻撃の機会を伺う様子見ステート。
    /// TestEnemy の StrafeState に相当する。
    /// 一定時間後に攻撃ステートへ遷移する。
    /// 単に左右へ揺れるだけでなく、間合いが近すぎれば下がり、遠すぎれば詰め、
    /// 適正距離ではプレイヤーの視野外側へ回り込むように動く（間合い管理）。
    /// </summary>
    public class TBossWatchState : TBossBaseState
    {
        // プレイヤーへ向き直る補間速度
        private const float RotateSpeed = 8f;

        // 様子見時間にランダム幅を持たせ、読まれやすい一定間隔での攻撃を避ける
        private const float WatchDurationMinScale = 0.6f;
        private const float WatchDurationMaxScale = 1.4f;

        // 近すぎる/遠すぎるとみなす距離のAttackRange・NormalAttack2Rangeに対する倍率
        private const float TooCloseRangeScale = 0.8f;
        private const float TooFarRangeScale = 0.9f;
        // プレイヤーが弓を構えている間は、より遠くまで「適正距離」とみなして距離を取る
        private const float TooFarRangeScaleWhileAiming = 1.3f;

        private float _watchTimer;
        private float _watchDurationThisEntry;
        private float _strafeDirection;
        private PlayerController _playerController;

        public TBossWatchState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 様子見ステート開始");

            _watchTimer = 0f;
            _watchDurationThisEntry = Random.Range(
                boss.WatchDuration * WatchDurationMinScale,
                boss.WatchDuration * WatchDurationMaxScale
            );

            _strafeDirection = DecideInitialStrafeDirection();
            _playerController = boss.Player != null
                ? boss.Player.GetComponent<PlayerController>()
                : null;

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.StrafeSpeed;
                boss.Agent.isStopped = false;
                // NavMeshAgentの自動回転を切ることで、自前でプレイヤー方向を向きながら移動できる
                boss.Agent.updateRotation = false;
            }

            if (boss.Animator != null)
            {
                // 走るモーションを解除（歩きモーション等への遷移を促す）
                boss.Animator.SetBool("IsRunning", false);
            }
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            // 見失い判定
            if (!boss.CheckInFlashlight())
            {
                boss.LostSightTimer += Time.deltaTime;
                if (boss.LostSightTimer >= boss.LostSightDuration)
                {
                    boss.LostSightTimer = 0f;
                    // 即座にパトロールへ戻さず、最後に把握した位置を捜索してから諦めさせる
                    boss.ChangeState(new TBossSearchState(boss, boss.LastKnownPlayerPosition));
                    return;
                }
            }
            else
            {
                boss.LostSightTimer = 0f;
            }

            _watchTimer += Time.deltaTime;

            // 様子見時間が経過したら攻撃へ
            if (_watchTimer >= _watchDurationThisEntry)
            {
                boss.TransitionToAttack();
                return;
            }

            // プレイヤーとの間合いを管理しながら移動
            ManageSpacing();
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 様子見ステート終了");

            if (boss.Agent != null)
            {
                if (boss.Agent.isOnNavMesh)
                {
                    boss.Agent.ResetPath();
                }

                // NavMeshから外れた瞬間に抜けても以降のステートで回転が死なないよう、無条件で戻す
                boss.Agent.updateRotation = true;
            }
        }

        /// <summary>
        /// 開始時点でのボスの位置がプレイヤーの正面寄りなら、より外側（視野の端・背後）へ
        /// 回り込む方向を初期ストレイフ方向として選ぶ。プレイヤーの視野外に立つ方が
        /// 「回り込まれている」緊張感を出せるため、単純なランダムより優先する。
        /// </summary>
        private float DecideInitialStrafeDirection()
        {
            if (boss.Player == null)
            {
                return Random.value > 0.5f ? 1f : -1f;
            }

            Vector3 toBossFromPlayer = boss.transform.position - boss.Player.transform.position;
            toBossFromPlayer.y = 0f;
            if (toBossFromPlayer.sqrMagnitude < 0.0001f)
            {
                return Random.value > 0.5f ? 1f : -1f;
            }

            float angle = Vector3.SignedAngle(
                boss.Player.transform.forward,
                toBossFromPlayer.normalized,
                Vector3.up
            );
            return angle >= 0f ? 1f : -1f;
        }

        private void ManageSpacing()
        {
            if (boss.Agent == null || boss.Player == null)
            {
                return;
            }

            // 水平成分だけを見る。y成分を残すとLookRotationでボスが前後に傾いてしまう
            Vector3 dirToPlayer = boss.Player.transform.position - boss.transform.position;
            dirToPlayer.y = 0f;
            float distance = dirToPlayer.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }
            dirToPlayer.Normalize();

            bool playerAiming = _playerController != null && _playerController.IsAiming;

            float tooCloseDistance = boss.AttackRange * TooCloseRangeScale;
            float tooFarDistance =
                boss.NormalAttack2Range
                * (playerAiming ? TooFarRangeScaleWhileAiming : TooFarRangeScale);

            Vector3 targetPos;

            if (distance < tooCloseDistance)
            {
                // 近すぎるので後退
                targetPos = boss.transform.position - dirToPlayer * 2f;
            }
            else if (distance > tooFarDistance)
            {
                // 遠すぎるので詰める
                targetPos = boss.transform.position + dirToPlayer * 2f;
            }
            else
            {
                // 適正距離。プレイヤーの視野外側へ回り込むようにストレイフする
                Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;

                // 壁があれば方向反転
                Vector3 rayStart = boss.transform.position + Vector3.up * 1f;
                if (
                    Physics.Raycast(rayStart, strafeDir, out RaycastHit hit, 2f, boss.ObstacleLayer)
                )
                {
                    _strafeDirection *= -1f;
                    strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
                }

                targetPos = boss.transform.position + strafeDir * 2f;
            }

            boss.Agent.SetDestination(targetPos);

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(dirToPlayer),
                Time.deltaTime * RotateSpeed
            );
        }
    }
}
