using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 少し踏み込んで攻撃する近接攻撃ステート。
    /// TestEnemyのAttackStateとほぼ同構造。
    /// 当たり判定はAnimationEventで制御する。
    /// </summary>
    public class TBossNormalAttackState : TBossBaseState
    {
        // ホーミング（方向補正）区間
        private const float HomingThreshold = 0.4f;

        // 踏み込み区間
        private const float LungeStartThreshold = 0.4f;
        private const float LungeEndThreshold = 0.7f;

        private const float HomingSpeed = 10f;
        private const float LungeSpeed = 8f;

        private bool _isApproaching;

        public TBossNormalAttackState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 通常攻撃ステート開始");

            if (
                boss.Player != null
                && Vector3.Distance(boss.transform.position, boss.Player.transform.position)
                    > boss.AttackRange
            )
            {
                _isApproaching = true;
                if (boss.Agent != null)
                {
                    boss.Agent.speed = boss.RunSpeed;
                    boss.Agent.isStopped = false;
                }
                if (boss.Animator != null)
                {
                    boss.Animator.SetBool("IsRunning", true);
                }
            }
            else
            {
                StartAttack();
            }
        }

        private void StartAttack()
        {
            _isApproaching = false;

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", false);
                boss.Animator.SetTrigger("Attack");
            }

            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, true);
            }
        }

        public override void Update()
        {
            if (_isApproaching)
            {
                if (boss.Player == null)
                {
                    boss.ChangeState(new TBossPatrolState(boss));
                    return;
                }

                // 懐中電灯をプレイヤーに向ける
                boss.RotateFlashlightToward(boss.Player.transform.position);

                if (boss.Agent != null)
                {
                    boss.Agent.SetDestination(boss.Player.transform.position);
                }

                if (
                    Vector3.Distance(boss.transform.position, boss.Player.transform.position)
                    <= boss.AttackRange
                )
                {
                    StartAttack();
                }
                return;
            }

            if (boss.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName("Attack"))
            {
                return;
            }

            // ホーミング区間：プレイヤー方向へ向き補正
            if (stateInfo.normalizedTime < HomingThreshold && boss.Player != null)
            {
                Vector3 dirToPlayer = (
                    boss.Player.transform.position - boss.transform.position
                ).normalized;
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                    boss.transform.rotation = Quaternion.Slerp(
                        boss.transform.rotation,
                        targetRotation,
                        Time.deltaTime * HomingSpeed
                    );
                }
            }
            // 踏み込み区間
            else if (
                stateInfo.normalizedTime >= LungeStartThreshold
                && stateInfo.normalizedTime <= LungeEndThreshold
            )
            {
                if (boss.Agent != null)
                {
                    boss.Agent.Move(boss.transform.forward * (LungeSpeed * Time.deltaTime));
                }
            }

            // アニメーション終了後の遷移
            if (stateInfo.normalizedTime >= 1.0f)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        public override void Exit()
        {
            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, false);
            }

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }
        }
    }
}
