using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 毎行動サイクルの起点となるステート。
    /// プレイヤーとの距離を確認し、攻撃か移動かを即座に選択して遷移する。
    /// このステートは Enter 内で完結し、Update は使用しない。
    /// </summary>
    public class MidBossStartState : MidBossBaseState
    {
        public MidBossStartState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] Startステート");

            if (con.Player == null)
            {
                // プレイヤーが存在しない場合は待機
                con.ChangeState(new MidBossWaitState(con, 1f, new MidBossStartState(con)));
                return;
            }

            float dist = con.DistanceToPlayer();

            if (dist <= con.AttackRange)
            {
                // 近接範囲内：低確率で攻撃、外れはランダム移動（追跡は含めない）
                if (Random.value <= con.AttackTriggerChance)
                {
                    con.TransitionToMeleeAttack();
                }
                else
                {
                    con.ChangeState(new MidBossRandomMoveState(con));
                }
            }
            else if (dist <= con.RangedAttackRange)
            {
                // 遠距離範囲内（近接範囲外）：低確率で遠距離攻撃、外れは追跡orランダム移動
                if (Random.value <= con.AttackTriggerChance)
                {
                    con.TransitionToRangedAttack();
                }
                else
                {
                    con.TransitionToMoveState();
                }
            }
            else
            {
                // 全範囲外：追跡 or ランダム移動
                con.TransitionToMoveState();
            }
        }
    }
}
