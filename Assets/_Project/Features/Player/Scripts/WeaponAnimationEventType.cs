namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃クリップの AnimationEvent から武器へ伝える合図の種類。
    /// クリップ上の関数名との対応は <see cref="PlayerAnimationEventRelay"/> を参照。
    /// </summary>
    public enum WeaponAnimationEventType
    {
        /// <summary>攻撃判定を有効にする(関数名: TriggerEnableHitbox)。</summary>
        HitboxOn,

        /// <summary>攻撃判定を無効にする(関数名: TriggerDisableHitbox)。</summary>
        HitboxOff,

        /// <summary>このフレーム以降、先行入力済みなら次段へ繋ぐ(関数名: ComboWindowOpen)。</summary>
        ComboWindowOpen,

        /// <summary>このフレーム以降、移動入力で硬直をキャンセルできる(関数名: RecoveryStart)。</summary>
        RecoveryStart,

        /// <summary>投擲物を手から離す(関数名: ThrowRelease)。</summary>
        ThrowRelease,

        /// <summary>行動の終わり。ここで操作を返し、移動モーションへ戻す(関数名: ActionEnd)。</summary>
        ActionEnd,

        /// <summary>弓を引き切った。矢をつがえて発射可能にする(関数名: DrawComplete)。</summary>
        DrawComplete,

        /// <summary>弓の発射後の戻りが終わった。次の矢を引ける(関数名: ReleaseEnd)。</summary>
        ReleaseEnd,
    }
}
