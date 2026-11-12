using System;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 入手した装備品(フィールドで拾う・イベントでもらう)の能力値の抽選。装備品の種類に依らない。
    /// 付与数(1〜2) → 型(重み付き非復元抽出) → 値(型ごとの4段階から等確率)。
    /// </summary>
    public static class DropStatRoller
    {
        /// <summary>入手した装備品に付く能力値の数の上限。</summary>
        public const int MaxDropStatCount = 2;

        /// <summary>
        /// 付く型の抽選重み(攻撃%/防御%/最大HP% 各2、会心ダメージ/会心率 各1 = 合計8)。
        /// 列挙順を固定して抽選を再現可能にするため配列にしている。食材専用の HealAmount は含めない。
        /// </summary>
        public static readonly IReadOnlyList<(StatType Type, int Weight)> DropStatWeights = new[]
        {
            (StatType.AttackPct, 2),
            (StatType.DefensePct, 2),
            (StatType.MaxHpPct, 2),
            (StatType.CritDamage, 1),
            (StatType.CritRate, 1),
        };

        /// <summary>型ごとの値の4段階。入手時はこの中から等確率で選ぶ。</summary>
        public static readonly IReadOnlyDictionary<StatType, IReadOnlyList<float>> DropStatTiers =
            new Dictionary<StatType, IReadOnlyList<float>>
            {
                { StatType.AttackPct, new[] { 4f, 6f, 8f, 10f } },
                { StatType.DefensePct, new[] { 4f, 6f, 8f, 10f } },
                { StatType.MaxHpPct, new[] { 4f, 6f, 8f, 10f } },
                { StatType.CritRate, new[] { 5f, 10f, 15f, 20f } },
                { StatType.CritDamage, new[] { 10f, 20f, 30f, 40f } },
            };

        // 装備品の種類に依らない。付与数(1〜2) → 型(重み付き非復元抽出) → 値(型ごとの4段階から等確率)。

        /// <summary>付与数の抽選。1つ / 2つ を等確率で選ぶ。</summary>
        public static int RollDropStatCount(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            return random.NextDouble() < 0.5 ? 1 : MaxDropStatCount;
        }

        /// <summary>入手した装備品の能力値を抽選する。付与数も抽選する。</summary>
        public static StatVector RollDropStats(IRandomSource random) =>
            RollDropStats(random, RollDropStatCount(random));

        /// <param name="statCount">付与数。0〜<see cref="MaxDropStatCount"/> に丸める。</param>
        public static StatVector RollDropStats(IRandomSource random, int statCount)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var values = new Dictionary<StatType, float>();
            foreach (var type in RollDropStatTypes(random, statCount))
            {
                var tiers = DropStatTiers[type];
                int index = Math.Min((int)(random.NextDouble() * tiers.Count), tiers.Count - 1);
                values[type] = tiers[index];
            }
            return new StatVector(values);
        }

        /// <summary>付く型を重み付き非復元抽出で選ぶ(同じ型は重複しない)。選ばれた順に返す。</summary>
        /// <param name="count">選ぶ型の数。0〜<see cref="MaxDropStatCount"/> に丸める。</param>
        public static IReadOnlyList<StatType> RollDropStatTypes(
            IRandomSource random,
            int count = MaxDropStatCount
        )
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int n = Math.Clamp(count, 0, MaxDropStatCount);
            var picked = new List<StatType>(n);
            var remaining = new List<(StatType Type, int Weight)>(DropStatWeights);
            int totalWeight = 0;
            foreach (var w in remaining)
                totalWeight += w.Weight;

            for (int i = 0; i < n && remaining.Count > 0; i++)
            {
                // [0, totalWeight) の一様値を累積重みで引く。境界の丸めで溢れても最後の候補に落とす。
                double r = random.NextDouble() * totalWeight;
                int index = remaining.Count - 1;
                double acc = 0;
                for (int j = 0; j < remaining.Count; j++)
                {
                    acc += remaining[j].Weight;
                    if (r < acc)
                    {
                        index = j;
                        break;
                    }
                }

                picked.Add(remaining[index].Type);
                totalWeight -= remaining[index].Weight; // 非復元: 選んだ型のぶん母数を減らす
                remaining.RemoveAt(index);
            }

            return picked;
        }
    }
}
