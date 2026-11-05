using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// AI制御と物理を停止させる終端ステート。TestEnemyDeathStateと同構造。
    /// </summary>
    public class TBossDeathState : TBossBaseState
    {
        private static readonly int DieTriggerHash = Animator.StringToHash("Die");

        public TBossDeathState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 死亡ステート開始");

            if (boss.Agent != null)
            {
                boss.Agent.enabled = false;
            }

            if (boss.EnemyCollider != null)
            {
                boss.EnemyCollider.enabled = false;
            }

            boss.StopSwingEffects();

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger(DieTriggerHash);
            }
        }

        public override void Update() { }
    }
}
