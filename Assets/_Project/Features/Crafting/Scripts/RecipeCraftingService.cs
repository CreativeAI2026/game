using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// CraftRecipe のレシピで調合する。素材はインベントリのアイテムとして消費するだけで、
    /// アイテム使用時の効果は発動しない。
    /// </summary>
    public class RecipeCraftingService
    {
        private readonly InventoryService _inventoryService;

        public RecipeCraftingService(InventoryService inventoryService)
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

        /// <summary>素材を1個ずつ消費して完成品を1つ付与する。作れなければ何も変えずに false。</summary>
        public bool TryCraft(CraftRecipe recipe, ItemStack materialA, ItemStack materialB)
        {
            if (!CanCraft(recipe, materialA, materialB))
                return false;

            if (
                !_inventoryService.ConsumeFromStack(materialA, 1)
                || !_inventoryService.ConsumeFromStack(materialB, 1)
            )
                return false;

            GrantResult(recipe);
            return true;
        }

        /// <summary>
        /// 結果アイテムを1つ付与する。装備品は個体差ロールした個体を作り、食材など非装備品はそのまま追加する。
        /// </summary>
        private void GrantResult(CraftRecipe recipe)
        {
            if (recipe.resultItem is EquipmentData)
            {
                var rolled = RollCraftedStats(
                    EquipmentData.ToStatVector(recipe.material1 as EquipmentData),
                    EquipmentData.ToStatVector(recipe.material2 as EquipmentData),
                    new SystemRandomSource()
                );
                _inventoryService.AddInstance(recipe.resultItem, RolledStat.FromVector(rolled));
            }
            else
            {
                _inventoryService.AddItem(recipe.resultItem, 1);
            }
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
            if (a is WeaponData || b is WeaponData)
                return false;
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
}
