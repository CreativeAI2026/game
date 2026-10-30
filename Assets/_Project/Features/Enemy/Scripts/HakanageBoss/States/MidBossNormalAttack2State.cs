using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 通常攻撃2ステート（保留中）。
    /// 現時点では空のスタブ実装。Startステートの選択肢には含めない。
    /// </summary>
    public class MidBossNormalAttack2State : MidBossBaseState
    {
        public MidBossNormalAttack2State(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 通常攻撃2ステート開始（未実装）");
            // TODO: 通常攻撃2の実装（保留）
            con.ChangeState(
                new MidBossWaitState(con, con.WaitAfterAttackDuration, new MidBossStartState(con))
            );
        }
    }
}
