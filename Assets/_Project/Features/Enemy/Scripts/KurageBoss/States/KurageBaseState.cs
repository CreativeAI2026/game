namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの全ステートが共有する基底クラス。
    /// アニメーションイベント(<see cref="KurageAnimationEvent"/>)は今のステートへ届くので、使うステートだけ受け取る。
    /// </summary>
    public abstract class KurageBaseState : IEnemyState
    {
        protected readonly KurageBossController boss;

        protected KurageBaseState(KurageBossController controller)
        {
            boss = controller;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void LateUpdate() { }

        public virtual void Exit() { }

        public virtual void OnAnimationEvent(KurageAnimationEvent animationEvent) { }

        /// <summary>正面をステージへ向ける。プレイヤーを目で追うことはしない(移動のクリップは正面を向いたまま左右へ動く作り)。</summary>
        protected void FaceStage() => boss.Mover.FaceStage(boss.TurnSpeed);
    }
}
