namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 死亡。動きと当たり判定を止める終端ステート。
    /// 死亡のクリップは後から入る。Animator の Die ステートにクリップを入れれば、ここは変えずに再生される。
    /// </summary>
    public class KurageDeathState : KurageBaseState
    {
        public KurageDeathState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            if (boss.EnemyCollider != null)
                boss.EnemyCollider.enabled = false;
            boss.StabHitbox.DisableHitbox();
            boss.Animator.SetTrigger(KurageAnimatorParams.Die);
        }
    }
}
