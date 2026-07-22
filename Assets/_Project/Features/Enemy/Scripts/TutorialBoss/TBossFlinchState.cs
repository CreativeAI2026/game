using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃を受けて怯むステート。TestEnemyFlinchStateと同構造。
    /// </summary>
    public class TBossFlinchState : TBossBaseState
    {
        public TBossFlinchState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 怯みステート開始");

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.velocity = Vector3.zero;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Flinch");
            }
        }

        public override void Update()
        {
            if (boss.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Flinch") && stateInfo.normalizedTime >= 1.0f)
            {
                if (boss.IsAlerted)
                {
                    boss.ChangeState(new TBossChaseState(boss));
                }
                else
                {
                    boss.ChangeState(new TBossPatrolState(boss));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 怯みステート終了");
        }
    }
}
