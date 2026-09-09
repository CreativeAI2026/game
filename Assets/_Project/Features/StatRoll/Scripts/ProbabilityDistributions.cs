using System;

namespace CreativeAI.StatRoll
{
    /// <summary>
    /// 配分ロール(StatRollAlgorithm.md 層2)で使う確率分布のサンプラ。
    /// ガンマ → ディリクレ の順に構成する。
    /// </summary>
    public static class ProbabilityDistributions
    {
        /// <summary>
        /// 形状 shape(&gt;0)・尺度1 のガンマ分布から標本を1つ得る。
        /// Marsaglia &amp; Tsang 法。
        /// </summary>
        public static double SampleGamma(double shape, IRandomSource rng)
        {
            if (shape <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(shape), "shape must be > 0");

            // shape < 1 はブースト変換で shape+1 に帰着させる。
            if (shape < 1.0)
            {
                double u = rng.NextDouble();
                while (u <= double.Epsilon)
                    u = rng.NextDouble();
                return SampleGamma(shape + 1.0, rng) * Math.Pow(u, 1.0 / shape);
            }

            double d = shape - 1.0 / 3.0;
            double c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                double x = rng.NextGaussian();
                double v = 1.0 + c * x;
                if (v <= 0.0)
                    continue;
                v = v * v * v;
                double u = rng.NextDouble();
                double x2 = x * x;
                if (u < 1.0 - 0.0331 * x2 * x2)
                    return d * v;
                if (Math.Log(u) < 0.5 * x2 + d * (1.0 - v + Math.Log(v)))
                    return d * v;
            }
        }

        /// <summary>
        /// ディリクレ分布 Dirichlet(alpha) から確率ベクトルをロールする。
        /// 各成分を Gamma(alpha_i) でサンプルして総和で正規化する。
        /// </summary>
        public static double[] SampleDirichlet(double[] alpha, IRandomSource rng)
        {
            var samples = new double[alpha.Length];
            double sum = 0.0;
            for (int i = 0; i < alpha.Length; i++)
            {
                samples[i] = SampleGamma(alpha[i], rng);
                sum += samples[i];
            }

            if (sum <= 0.0)
            {
                // 退化ケース(全標本が0)。均等配分にフォールバック。
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = 1.0 / samples.Length;
                return samples;
            }

            for (int i = 0; i < samples.Length; i++)
                samples[i] /= sum;
            return samples;
        }
    }
}
