namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボスのクリップに置くアニメーションイベントの種類。
    /// クリップからはコントローラの同名メソッド（TriggerEnableHitbox など）が呼ばれ、今のステートへこの値で渡される。
    /// </summary>
    public enum TBossAnimationEvent
    {
        /// <summary>通常攻撃の当たり判定を入れる。</summary>
        MeleeHitStart,

        /// <summary>通常攻撃の当たり判定を切る。</summary>
        MeleeHitEnd,

        /// <summary>鎌の軌跡を出し始める。</summary>
        ScytheTrailShow,

        /// <summary>鎌の軌跡を止める。</summary>
        ScytheTrailHide,

        /// <summary>特殊攻撃で、ワイヤーを射出するのに一番適したポーズになった。</summary>
        SpecialAttackPoseReady,

        /// <summary>通常攻撃2で、腕を伸ばして振りに入る。1クリップ内で複数回来る。</summary>
        Attack2ReachExtend,

        /// <summary>通常攻撃2で、腕を戻し終えた。1クリップ内で複数回来る。</summary>
        Attack2ReachRetract,

        /// <summary>通常攻撃2で、伸びた腕全体の当たり判定を入れる。</summary>
        Attack2ArmSweepStart,

        /// <summary>通常攻撃2で、伸びた腕全体の当たり判定を切る。</summary>
        Attack2ArmSweepEnd,

        /// <summary>通常攻撃2で、振り下ろした鎌が地面に刺さった。</summary>
        Attack2GroundImpact,

        /// <summary>通常攻撃2で、地面に刺した鎌を引きずり始める。</summary>
        Attack2GroundDragStart,

        /// <summary>通常攻撃2で、鎌が地面から離れる。</summary>
        Attack2GroundDragEnd,
    }
}
