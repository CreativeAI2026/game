using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを発見した状態での追跡ステート。
    /// 一定時間光にプレイヤーが入らなければ見失い、パトロールへ戻る。
    /// 攻撃範囲内に入ったら様子見→攻撃へ遷移する。
    /// </summary>
    public class TBossChaseState : TBossBaseState
    {
        public TBossChaseState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 追跡ステート開始");

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.RunSpeed;
                boss.Agent.isStopped = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", true);
            }

            boss.LostSightTimer = 0f;
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            bool inSight = boss.CheckInFlashlight();

            if (!inSight)
            {
                boss.LostSightTimer += Time.deltaTime;

                // 視界外なら移動を渐渐減速し、完全に見失ったら停止
                if (boss.LostSightTimer >= boss.LostSightDuration * 0.5f)
                {
                    boss.Agent.isStopped = true;
                }

                if (boss.LostSightTimer >= boss.LostSightDuration)
                {
                    boss.IsAlerted = false;
                    boss.LostSightTimer = 0f;
                    boss.Agent.isStopped = false;
                    boss.ChangeState(new TBossPatrolState(boss));
                    return;
                }
            }
            else
            {
                boss.LostSightTimer = 0f;
                boss.Agent.isStopped = false;

                // プレイヤーを追跡
                if (boss.Agent != null)
                {
                    boss.Agent.SetDestination(boss.Player.transform.position);
                }

                float distance = Vector3.Distance(
                    boss.transform.position,
                    boss.Player.transform.position
                );

                // 攻撃範囲内（特殊攻撃射程の半分、または通常攻撃の長い方）に入ったら様子見へ
                float attackThreshold = Mathf.Max(boss.AttackRange, boss.SpecialAttackRange * 0.5f);
                if (distance <= attackThreshold)
                {
                    boss.ChangeState(new TBossWatchState(boss));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 追跡ステート終了");

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", false);
            }
        }
    }
}
