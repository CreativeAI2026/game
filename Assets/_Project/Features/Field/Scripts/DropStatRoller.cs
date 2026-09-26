using System;
using System.Collections.Generic;
using CreativeAI.StatRoll;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドの装備品(合成前のシード)を拾ったときの個体ステータスを決めるロール。調合と同じ「総パワー × ディリクレ配分」で、
    /// 総パワー = シードの固定ステータス合計(C_cap でクランプ)、型 = <see cref="DropStatTypeRoller"/>(最大2つ)、配分の期待値 = 均等。
    /// 食材はこのモデルを通さない。
    /// </summary>
    public static class DropStatRoller
    {
        /// <summary>シードの総パワーから付与ステータス(型 + 量)を1個ぶんロールする。</summary>
        /// <param name="seedPower">シード装備品の固定ステータス合計(= 総パワー)。</param>
        /// <param name="statCount">付与数。既定は上限の2(範囲外は丸める)。</param>
        public static StatVector Roll(
            double seedPower,
            IRandomSource rng,
            CraftingParameters parameters = null,
            int statCount = DropStatTypeRoller.MaxStatCount
        )
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var p = parameters ?? CraftingParameters.Default;

            // --- 総パワー B: シードの宣言値。上限は調合と共用の C_cap ---
            double budget = Math.Min(seedPower, p.PowerCap);
            if (budget <= 0.0)
                return StatVector.Empty;

            // --- 型: 重み付き非復元抽出(どの型が付くかだけを決める) ---
            var types = DropStatTypeRoller.Roll(rng, Math.Min(statCount, p.MaxStatCount));
            if (types.Count == 0)
                return StatVector.Empty;

            // --- 配分 p: 親が居ないので ŵ は均等。α_s = α0 / n ---
            double[] shares =
                types.Count == 1
                    ? new[] { 1.0 }
                    : ProbabilityDistributions.SampleDirichlet(
                        UniformAlpha(types.Count, p.Alpha0),
                        rng
                    );

            // --- 合成 r_s = B · p_s、整理(ε切り捨て・型ごとのクランプ) ---
            var result = new Dictionary<StatType, float>();
            for (int i = 0; i < types.Count; i++)
            {
                double value = budget * shares[i];
                if (value < p.Epsilon)
                    continue; // 実質1型扱い: 微小成分は捨てる

                if (p.Caps.TryGetValue(types[i], out var cap))
                    value = Math.Min(value, cap);

                result[types[i]] = (float)value;
            }

            return new StatVector(result);
        }

        private static double[] UniformAlpha(int count, double alpha0)
        {
            var alpha = new double[count];
            for (int i = 0; i < count; i++)
                alpha[i] = alpha0 / count;
            return alpha;
        }
    }
}
