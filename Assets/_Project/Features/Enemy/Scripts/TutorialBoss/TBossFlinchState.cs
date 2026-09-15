using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃を受けて怯むステート。TestEnemyFlinchStateと同構造。
    /// </summary>
    public class TBossFlinchState : TBossBaseState
    {
        // Flinchステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float FlinchStateTimeout = 3f;

        private float _timer;

        public TBossFlinchState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 怯みステート開始");

            _timer = 0f;

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
            _timer += Time.deltaTime;

            if (boss.Animator == null)
            {
                ReturnToCombat();
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Flinch") && stateInfo.normalizedTime >= 1.0f)
            {
                ReturnToCombat();
                return;
            }

            // Flinchトリガーの取りこぼしで永久に怯んだままになるのを防ぐ
            if (_timer >= FlinchStateTimeout)
            {
                Debug.LogWarning(
                    "[TutorialBoss] Flinchステートが完了しませんでした。タイムアウトで復帰します。"
                );
                ReturnToCombat();
            }
        }

        private void ReturnToCombat()
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

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 怯みステート終了");
        }
    }
}
