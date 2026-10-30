using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 死亡ステート。TestEnemyDeathState と同等の実装。
    /// AI制御と物理演算を停止する終端ステート。
    /// </summary>
    public class MidBossDeathState : MidBossBaseState
    {
        public MidBossDeathState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 死亡ステート開始");

            if (con.Agent != null)
            {
                con.Agent.enabled = false;
            }

            if (con.EnemyCollider != null)
            {
                con.EnemyCollider.enabled = false;
            }

            // TODO: アニメーション導入時はここで Animator.SetTrigger("Die") を追加
        }

        public override void Update() { }
    }
}
