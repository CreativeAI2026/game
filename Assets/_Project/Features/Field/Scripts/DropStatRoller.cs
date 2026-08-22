using System;
using System.Collections.Generic;
using CreativeAI.StatRoll;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドに落ちている装備品(合成前のシード)を拾ったときの個体ステータスを決めるロール
    /// (documents/Specification.md §2.1.1 / StatRollAlgorithm.md「ドロップ(拾得)のロール」)。
    ///
    /// 調合(CraftingStatRoller)と同じ「総パワー × ディリクレ配分」だが、親が居ないので:
    /// - 総パワー B = シードの固定ステータス合計(C_cap でクランプ)
    /// - 対象の型 = <see cref="DropStatTypeRoller"/> の重み付き非復元抽出(最大2つ)
    /// - 配分の期待値 = 均等(ŵ = 1/n。偏らせる根拠が無いため)
    ///
    /// 食材はこのモデルを通さない(HP即時回復の固定ルール)。
    /// </summary>
    public static class DropStatRoller
    {
        /// <summary>
        /// シードの総パワーから付与ステータス(型 + 量)を1個ぶんロールする。
        /// </summary>
        /// <param name="seedPower">シード装備品の固定ステータス合計(= 総パワーの宣言)。</param>
        /// <param name="rng">乱数源。テストでは決定的な実装を差し込む。</param>
        /// <param name="parameters">調整ノブ。未指定なら既定値。</param>
        /// <param name="statCount">付与数。既定は上限の2(0 未満・上限超は丸める)。</param>
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
