namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 怯み。Animator の Flinch ステートを再生し、完了したら周回に戻る。
    /// </summary>
    public class KurageFlinchState : KurageBaseState
    {
        public KurageFlinchState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            boss.Animator.SetTrigger(KurageAnimatorParams.Flinch);
        }

        public override void Update()
        {
            boss.Mover.Move(0f, 0f);
            if (IsAnimationFinished(KurageAnimatorParams.Flinch))
                boss.ChangeState(new KurageOrbitState(boss));
        }
    }
}
