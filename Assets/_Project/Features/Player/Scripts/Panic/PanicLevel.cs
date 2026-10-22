namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度の段階。AI は数値ではなくこの段階で判断する（数値のまま使うと毎フレーム判断が揺れるため）。
    /// 値の大小で比較するので、並び順は弱い順のまま変えない。
    /// </summary>
    public enum PanicLevel
    {
        /// <summary>平常</summary>
        Calm,

        /// <summary>動揺</summary>
        Tense,

        /// <summary>焦り</summary>
        Panic,
    }

    public static class PanicLevelExtensions
    {
        /// <summary>デバッグ表示・ログに使う日本語名。</summary>
        public static string ToDisplayName(this PanicLevel level) =>
            level switch
            {
                PanicLevel.Calm => "平常",
                PanicLevel.Tense => "動揺",
                PanicLevel.Panic => "焦り",
                _ => level.ToString(),
            };
    }
}
