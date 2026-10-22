namespace CreativeAI.Gameplay
{
    /// <summary>ボスの追い込みの状態。<see cref="TBossPanicPressure"/> が管理する。</summary>
    public enum TBossPressureMode
    {
        /// <summary>追い込みなし（相手が平常、または交戦していない）</summary>
        None,

        /// <summary>軽い追い込み（相手が動揺している）</summary>
        Tense,

        /// <summary>系統ごとの本格的な追い込み（相手が焦っている）</summary>
        Pressing,

        /// <summary>息継ぎ（追い込みすぎを防ぐため、一定時間追い込まない）</summary>
        Breather,
    }

    public static class TBossPressureModeExtensions
    {
        /// <summary>デバッグ表示・ログに使う日本語名。</summary>
        public static string ToDisplayName(this TBossPressureMode mode) =>
            mode switch
            {
                TBossPressureMode.None => "なし",
                TBossPressureMode.Tense => "軽い追い込み",
                TBossPressureMode.Pressing => "追い込み中",
                TBossPressureMode.Breather => "息継ぎ",
                _ => mode.ToString(),
            };
    }
}
