using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを発見した後、攻撃の機会を伺う様子見ステート。
    /// TestEnemy の StrafeState に相当する。
    /// 一定時間後に攻撃ステートへ遷移する。
    /// </summary>
    public class TBossWatchState : TBossBaseState
    {
        private float _watchTimer;
        private float _strafeDirection;

        public TBossWatchState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 様子見ステート開始");

            _watchTimer = 0f;
            _strafeDirection = Random.value > 0.5f ? 1f : -1f;

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

            // 懐中電灯をプレイヤーに向け続ける
            boss.RotateFlashlightToward(boss.Player.transform.position);

            // 見失い判定
            if (!boss.CheckInFlashlight())
            {
                boss.LostSightTimer += Time.deltaTime;
                if (boss.LostSightTimer >= boss.LostSightDuration)
                {
                    boss.IsAlerted = false;
                    boss.LostSightTimer = 0f;
                    boss.ChangeState(new TBossPatrolState(boss));
                    return;
                }
            }
            else
            {
                boss.LostSightTimer = 0f;
            }

            _watchTimer += Time.deltaTime;

            // 様子見時間が経過したら攻撃へ
            if (_watchTimer >= boss.WatchDuration)
            {
                boss.TransitionToAttack();
                return;
            }

            // プレイヤーを向きながら左右に移動
            Strafe();
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 様子見ステート終了");

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.Agent.updateRotation = true;
            }
        }

        private void Strafe()
        {
            if (boss.Agent == null || boss.Player == null)
            {
                return;
            }

            Vector3 dirToPlayer = (
                boss.Player.transform.position - boss.transform.position
            ).normalized;

            Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;

            // 壁があれば方向反転
            Vector3 rayStart = boss.transform.position + Vector3.up * 1f;
            if (Physics.Raycast(rayStart, strafeDir, out RaycastHit hit, 2f, boss.ObstacleLayer))
            {
                _strafeDirection *= -1f;
                strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
            }

            Vector3 targetPos = boss.transform.position + strafeDir * 2f;
            boss.Agent.SetDestination(targetPos);
            boss.transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }
    }
}
