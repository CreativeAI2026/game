using System;

namespace CreativeAI.StatRoll
{
    /// <summary>
    /// 層1: 総パワー予算 B の算出(StatRollAlgorithm.md)。
    /// 「強い方を土台に、弱い方をボーナスで上限漸近させながら足す」。
    /// </summary>
    public static class PowerBudget
    {
        /// <summary>
        /// 減衰パワー予算(ソフトキャップ版)。
        /// B = base + (cap - base)(1 - e^(-β·sub/(cap - base)))
        /// 性質: B ≥ base(非劣化) かつ B &lt; cap(上限漸近)。
        /// </summary>
        public static double ComputeSoftCap(double powerA, double powerB, CraftingParameters p)
        {
            double bas = Math.Max(powerA, powerB);
            double sub = Math.Min(powerA, powerB);

            double headroom = p.PowerCap - bas;
            // 既に上限以上なら成長させない(非劣化のみ保証)。
            if (headroom <= 0.0)
                return bas;
            if (sub <= 0.0)
                return bas;

            double filled = headroom * (1.0 - Math.Exp(-p.Beta * sub / headroom));
            return bas + filled;
        }

        /// <summary>
        /// ハードキャップ版(簡易): B = min(cap, base + β·sub)。
        /// 同じ入出力なので後でソフトキャップと差し替え可能。
        /// </summary>
        public static double ComputeHardCap(double powerA, double powerB, CraftingParameters p)
        {
            double bas = Math.Max(powerA, powerB);
            double sub = Math.Min(powerA, powerB);
            return Math.Min(p.PowerCap, bas + p.Beta * sub);
        }
    }
}
