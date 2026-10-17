using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接攻撃をジャストパリィされて大きく崩れるステート。Animator の Parried モーションを最後まで再生してから交戦に戻る。
    /// 崩れている間は顔向けも切れるため（IsHeadAimActive が false）、プレイヤーは反撃するか視界から抜けるかを選べる。
    /// </summary>
    public class TBossParriedState : TBossBaseState
    {
        // Parriedステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float ParriedStateTimeout = 3f;

        private float _timer;

        public TBossParriedState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] パリィされたため大きく崩れる");

            _timer = 0f;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.Agent.velocity = Vector3.zero;
                boss.Agent.isStopped = true;
            }

            // 振りの途中で中断されるため、振り終わりのAnimationEventで行う後始末が呼ばれない。ここで代わりに止める
            if (boss.MeleeHitbox != null)
            {
                boss.MeleeHitbox.DisableHitbox();
            }
            boss.HideScytheTrail();

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Parried");
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
            if (stateInfo.IsName("Parried") && stateInfo.normalizedTime >= 1.0f)
            {
                ReturnToCombat();
                return;
            }

            if (!stateInfo.IsName("Parried") && _timer >= ParriedStateTimeout)
            {
                Debug.LogWarning(
                    "[TutorialBoss] Parriedステートに入れませんでした。タイムアウトで復帰します。"
                );
                ReturnToCombat();
            }
        }

        public override void Exit()
        {
            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = false;
            }
        }

        private void ReturnToCombat()
        {
            // パリィされるのは交戦中だけなので警戒は保ったまま追跡へ戻る（見えていなければ追跡側で見失い処理に入る）
            boss.IsAlerted = true;
            boss.ChangeState(new TBossChaseState(boss));
        }
    }
}
