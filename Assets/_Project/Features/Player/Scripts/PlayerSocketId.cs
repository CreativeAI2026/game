namespace CreativeAI.Gameplay
{
    /// <summary>
    /// モデル側に置く取り付け点(ソケット)の種類。武器や生成位置はこの ID でソケットを引く。
    /// ソケットの位置・向きはモデルごとに <see cref="PlayerModel"/> 側で調整するので、
    /// 武器側はモデル(ボーン名・ボーンの軸)を知らなくてよい。
    /// 新しい武器を足すときはここに ID を追加し、<see cref="PlayerModel.DefaultBoneOf"/> に既定の親ボーンを追加する。
    /// </summary>
    public enum PlayerSocketId
    {
        /// <summary>剣の持ち手(右手)。</summary>
        SwordGrip,

        /// <summary>弓の持ち手(左手)。</summary>
        BowGrip,

        /// <summary>鎌の持ち手(右手)。投擲の発射位置・受け取り位置も兼ねる。</summary>
        ScytheGrip,

        /// <summary>弓を構えたときに矢が最初に現れる位置(引き手 = 右手)。</summary>
        ArrowHold,
    }
}
