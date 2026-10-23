namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦りの系統。ボスが「何に焦っているか」に合わせて追い込み方を変えるため、指標をこの単位でまとめる。
    /// </summary>
    public enum PanicCategory
    {
        /// <summary>逃げ腰（逃避・空間喪失）</summary>
        Avoidance,

        /// <summary>防御崩れ（防御の連打・連続被弾）</summary>
        DefenseBreakdown,

        /// <summary>迷い（弓のキャンセル・照準のぶれ・武器の空回し）</summary>
        Indecision,

        /// <summary>硬直（攻撃が来ているのに何もできない）</summary>
        Freeze,
    }

    public static class PanicCategoryExtensions
    {
        /// <summary>デバッグ表示・ログの列名に使う日本語名。</summary>
        public static string ToDisplayName(this PanicCategory category) =>
            category switch
            {
                PanicCategory.Avoidance => "逃げ腰",
                PanicCategory.DefenseBreakdown => "防御崩れ",
                PanicCategory.Indecision => "迷い",
                PanicCategory.Freeze => "硬直",
                _ => category.ToString(),
            };
    }
}
