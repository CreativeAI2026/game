using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// CraftRecipe のレシピで調合する。素材はインベントリのアイテムとして消費するだけで、
    /// アイテム使用時の効果は発動しない。
    /// </summary>
    public class CraftingService
    {
        private readonly InventoryService _inventoryService;

        public CraftingService(InventoryService inventoryService)
        {
            _inventoryService =
                inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        /// <summary>
        /// 選んだ素材2つ(各1個)でレシピの完成品を作れるか。装備中・即時食材セット中の素材は使えない。
        /// </summary>
        public bool CanCraft(CraftRecipe recipe, ItemStack materialA, ItemStack materialB)
        {
            return IsValidRecipe(recipe)
                && CanUseAsMaterial(materialA)
                && CanUseAsMaterial(materialB)
                && materialA != materialB
                && materialA.Data != materialB.Data
                && recipe.MatchesMaterials(materialA.Data, materialB.Data);
        }

        /// <summary>
        /// 素材を1個ずつ消費して完成品を1つ付与する。作れなければ何も変えずに false。
        /// crafted は完成品が入った在庫のスタック(装備品はロール済み個体)。
        /// </summary>
        public bool TryCraft(
            CraftRecipe recipe,
            ItemStack materialA,
            ItemStack materialB,
            out ItemStack crafted
        )
        {
            crafted = null;
            if (!CanCraft(recipe, materialA, materialB))
                return false;

            if (
                !_inventoryService.ConsumeFromStack(materialA, 1)
                || !_inventoryService.ConsumeFromStack(materialB, 1)
            )
                return false;

            crafted = GrantResult(recipe, materialA, materialB);
            return true;
        }

        /// <summary>
        /// 結果アイテムを1つ付与する。装備品は個体差ロールした個体を作り、食材など非装備品はそのまま追加する。
        /// </summary>
        private ItemStack GrantResult(CraftRecipe recipe, ItemStack materialA, ItemStack materialB)
        {
            if (recipe.resultItem.category == ItemCategory.Equipment)
            {
                // 素材にした2つの個体の能力値を元にする。
                var rolled = RollCraftedStats(
                    RolledStat.ToVector(materialA.RolledStats),
                    RolledStat.ToVector(materialB.RolledStats),
                    new SystemRandomSource()
                );
                return _inventoryService.AddInstance(
                    recipe.resultItem,
                    RolledStat.FromVector(rolled)
                );
            }

            return _inventoryService.AddItem(recipe.resultItem, 1);
        }

        private bool CanUseAsMaterial(ItemStack stack)
        {
            return stack != null
                && stack.Data != null
                && stack.Count > 0
                && !stack.IsEquipped
                && !_inventoryService.IsInQuickFood(stack)
                && _inventoryService.ContainsStack(stack);
        }

        private static bool IsValidRecipe(CraftRecipe recipe)
        {
            if (recipe?.resultItem == null)
                return false;

            var a = recipe.material1;
            var b = recipe.material2;
            if (a == null || b == null || a == b)
                return false;

            // 調合は「装備品同士 / 食材同士」のみ(武器・大事なもの・カテゴリ跨ぎは不可)。
            // UI 非経由の直呼びも弾くためサービス層で明示ガードする。
            return a.category == b.category
                && (a.category == ItemCategory.Equipment || a.category == ItemCategory.Food);
        }

        // --- 装備品の能力値の抽選 ---
        // 総パワー(強い方を土台に弱い方を上乗せ) × 配分(ディリクレ)で、ウェイト上位2型に割り振る。

        public static StatVector RollCraftedStats(
            StatVector a,
            StatVector b,
            IRandomSource rng,
            StatRollParameters parameters = null
        )
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var p = parameters ?? StatRollParameters.Default;
            double budget = ComputePowerBudget(a.Power, b.Power, p);
            if (budget <= 0.0)
                return StatVector.Empty;

            // 両親が共通で持つ型はシナジーで増幅する。
            var union = new HashSet<StatType>(a.Types);
            union.UnionWith(b.Types);
            if (union.Count == 0)
                return StatVector.Empty;

            var weights = new Dictionary<StatType, double>();
            foreach (var s in union)
            {
                double sum = a[s] + b[s];
                bool shared = a[s] > 0f && b[s] > 0f;
                weights[s] = sum * (1.0 + p.Synergy * (shared ? 1.0 : 0.0));
            }

            var top = weights
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key) // 同値は型順で安定化
                .Take(p.MaxStatCount)
                .ToList();

            double topSum = top.Sum(kv => kv.Value);
            if (topSum <= 0.0)
                return StatVector.Empty;

            // 候補が1型しかなければ抽選不要(全量をその型へ)。
            double[] shares =
                top.Count == 1
                    ? new[] { 1.0 }
                    : ProbabilityDistributions.SampleDirichlet(
                        top.Select(kv => p.Alpha0 * (kv.Value / topSum)).ToArray(),
                        rng
                    );

            return p.Distribute(top.Select(kv => kv.Key).ToList(), shares, budget);
        }

        /// <summary>
        /// 総パワー B = base + (cap - base)(1 - e^(-β·sub/(cap - base)))。
        /// base は強い方、sub は弱い方。B ≥ base(非劣化) かつ B &lt; cap(上限漸近)。
        /// </summary>
        public static double ComputePowerBudget(double powerA, double powerB, StatRollParameters p)
        {
            double bas = Math.Max(powerA, powerB);
            double sub = Math.Min(powerA, powerB);

            double headroom = p.PowerCap - bas;
            // 既に上限以上なら成長させない(非劣化のみ保証)。
            if (headroom <= 0.0 || sub <= 0.0)
                return bas;

            return bas + headroom * (1.0 - Math.Exp(-p.Beta * sub / headroom));
        }
    }

    /// <summary>
    /// 調合の能力値抽選の調整ノブ。値はバランス調整用。
    /// </summary>
    public sealed class StatRollParameters
    {
        /// <summary>β: ボーナス係数。弱い方の素材が上乗せする成長量。</summary>
        public double Beta { get; set; } = 0.5;

        /// <summary>C_cap: 総パワー上限。連鎖インフレを止める天井。</summary>
        public double PowerCap { get; set; } = 100.0;

        /// <summary>σ: シナジー強度。両親が共通で持つ型の増幅率。</summary>
        public double Synergy { get; set; } = 0.5;

        /// <summary>α0: 集中度。小さいほど尖り(当たり/はずれ)、大きいほど均等。</summary>
        public double Alpha0 { get; set; } = 6.0;

        /// <summary>ε: 切り捨て下限。これ未満の型は捨てて実質1型扱いにする。</summary>
        public double Epsilon { get; set; } = 1.0;

        /// <summary>付与数の上限(固定で2)。</summary>
        public int MaxStatCount => 2;

        /// <summary>型ごとの値の上限(例: 会心率 ≤ 100%)。未登録の型は無制限。</summary>
        public IReadOnlyDictionary<StatType, float> Caps { get; set; } =
            new Dictionary<StatType, float> { { StatType.CritRate, 100f } };

        public static StatRollParameters Default => new StatRollParameters();

        /// <summary>
        /// 総パワー budget を配分 shares で各型に割り振る。ε 未満は捨て、型ごとの上限でクランプする。
        /// </summary>
        public StatVector Distribute(
            IReadOnlyList<StatType> types,
            IReadOnlyList<double> shares,
            double budget
        )
        {
            var result = new Dictionary<StatType, float>();
            for (int i = 0; i < types.Count; i++)
            {
                double value = budget * shares[i];
                if (value < Epsilon)
                    continue; // 実質1型扱い: 微小成分は捨てる

                if (Caps.TryGetValue(types[i], out var cap))
                    value = Math.Min(value, cap);

                result[types[i]] = (float)value;
            }
            return new StatVector(result);
        }
    }

    /// <summary>配分の抽選で使う確率分布のサンプラ(ガンマ → ディリクレ)。</summary>
    public static class ProbabilityDistributions
    {
        /// <summary>形状 shape(&gt;0)・尺度1 のガンマ分布から標本を1つ得る(Marsaglia &amp; Tsang 法)。</summary>
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

        /// <summary>ディリクレ分布 Dirichlet(alpha) から確率ベクトルを抽選する。</summary>
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
