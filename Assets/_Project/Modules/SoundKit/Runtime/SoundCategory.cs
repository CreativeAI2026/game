namespace SoundKit
{
    /// <summary>
    /// 音の種類。どのミキサーグループへ流すか（＝どの音量設定に従うか）を決める。
    /// </summary>
    public enum SoundCategory
    {
        /// <summary>BGM（音楽）</summary>
        Bgm,

        /// <summary>環境音（稼働音・空調・場所の空気音など、鳴り続ける音）</summary>
        Ambient,

        /// <summary>効果音（足音・衝突・ヒットなど、出来事に合わせて鳴る音）</summary>
        Effect,

        /// <summary>UI音（選択・決定・知らせなど、画面の音）</summary>
        UI,
    }
}
