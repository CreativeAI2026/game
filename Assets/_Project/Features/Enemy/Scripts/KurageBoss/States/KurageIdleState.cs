namespace CreativeAI.Gameplay
{
    /// <summary>
    /// わっかでプレイヤーを締めている間の待機。追撃はせず、その場に浮いている。
    /// 拘束が終わったら(抜け出された・プレイヤーが倒れた)周回に戻る。
    /// </summary>
    public class KurageIdleState : KurageBaseState
    {
        public KurageIdleState(KurageBossController controller)
            : base(controller) { }

        public override void Update()
        {
            boss.Mover.Move(0f, 0f);
            FaceStage();

            if (!boss.Restraint.IsRestraining)
                boss.ChangeState(new KurageOrbitState(boss));
        }
    }
}
