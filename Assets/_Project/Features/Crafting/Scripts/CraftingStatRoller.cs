using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Crafting
{
    /// <summary>
    /// 調合ステータスのロール本体(CraftingStatAlgorithm.md)。
    /// 総パワー B(決定的) × 配分 p(ディリクレで確率的) を合成し、
    /// ウェイト上位2型に B を配分した結果ベクトルを返す。
    /// </summary>
    public sealed class CraftingStatRoller
    {
        private readonly CraftingParameters _p;

        public CraftingStatRoller(CraftingParameters parameters = null)
        {
            _p = parameters ?? CraftingParameters.Default;
        }

        public StatVector Roll(StatVector a, StatVector b, IRandomSource rng)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            // --- 層1: 総パワー予算 B ---
            double budget = PowerBudget.ComputeSoftCap(a.Power, b.Power, _p);
            if (budget <= 0.0)
                return StatVector.Empty;

            // --- 層2: 基礎ウェイト(シナジー増幅) ---
            var union = new HashSet<StatType>(a.Types);
            union.UnionWith(b.Types);
            if (union.Count == 0)
                return StatVector.Empty;

            var weights = new Dictionary<StatType, double>();
            foreach (var s in union)
            {
                double sum = a[s] + b[s];
                bool shared = a[s] > 0f && b[s] > 0f;
                weights[s] = sum * (1.0 + _p.Synergy * (shared ? 1.0 : 0.0));
            }

            // 付与数の上限(=2)までウェイト上位を採用。
            var top = weights
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key) // 同値は型順で安定化
                .Take(_p.MaxStatCount)
                .ToList();

            // --- 集中度ベクトル α = α0 · ŵ ---
            double topSum = top.Sum(kv => kv.Value);
            if (topSum <= 0.0)
                return StatVector.Empty;

            var alpha = top.Select(kv => _p.Alpha0 * (kv.Value / topSum)).ToArray();

            // 候補が1型しかなければロール不要(全量をその型へ)。
            double[] p;
            if (top.Count == 1)
                p = new[] { 1.0 };
            else
                p = ProbabilityDistributions.SampleDirichlet(alpha, rng);

            // --- 合成 r_s = B · p_s、整理(ε切り捨て・上限クランプ) ---
            var result = new Dictionary<StatType, float>();
            for (int i = 0; i < top.Count; i++)
            {
                double value = budget * p[i];
                if (value < _p.Epsilon)
                    continue; // 実質1型扱い: 微小成分は捨てる

                var type = top[i].Key;
                if (_p.Caps.TryGetValue(type, out var cap))
                    value = Math.Min(value, cap);

                result[type] = (float)value;
            }

            return new StatVector(result);
        }
    }
}
