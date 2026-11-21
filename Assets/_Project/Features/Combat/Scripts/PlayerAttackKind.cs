namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの攻撃の種類。敵が攻撃の受け止め方（防御・はじき返し）を種類で変えるために、攻撃側から渡す。
    /// </summary>
    public enum PlayerAttackKind
    {
        /// <summary>手に持った武器で振る攻撃（剣・ハンマー・鎌の3段攻撃）。</summary>
        Melee,

        /// <summary>ハンマーの叩きつけの範囲攻撃。武器そのものは当たらず、衝撃だけが届く。</summary>
        Slam,

        /// <summary>投げた鎌。</summary>
        Thrown,

        /// <summary>弓の矢。</summary>
        Arrow,
    }
}
