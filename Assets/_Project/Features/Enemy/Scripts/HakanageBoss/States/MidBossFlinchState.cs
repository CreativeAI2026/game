using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 怯みステート。TestEnemyFlinchState と同等の実装。
    /// Animator の "Flinch" Trigger でアニメーション再生し、完了後に StartState へ戻る。
    /// アニメーション未導入の場合は一定時間後に自動で次のステートへ遷移する。
    /// </summary>
    public class MidBossFlinchState : MidBossBaseState
    {
        // アニメーション未導入時のフォールバック待機時間
        private const float FallbackDuration = 0.8f;
        private float _fallbackTimer;
        private bool _animatorAvailable;

        public MidBossFlinchState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 怯みステート開始");

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }

            _fallbackTimer = 0f;
            _animatorAvailable = con.Animator != null;

            // TODO: アニメーション導入時はここで Animator.SetTrigger("Flinch") を追加し、
            //       _animatorAvailable フラグを true にする
            // if (con.Animator != null) con.Animator.SetTrigger("Flinch");
        }

        public override void Update()
        {
            if (_animatorAvailable && con.Animator != null)
            {
                AnimatorStateInfo stateInfo = con.Animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Flinch") && stateInfo.normalizedTime >= 1.0f)
                {
                    con.ChangeState(new MidBossStartState(con));
                }
            }
            else
            {
                // Animator 未導入時はタイマーで次のステートへ
                _fallbackTimer += UnityEngine.Time.deltaTime;
                if (_fallbackTimer >= FallbackDuration)
                {
                    con.ChangeState(new MidBossStartState(con));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("[MidBoss] 怯みステート終了");
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }
        }
    }
}
