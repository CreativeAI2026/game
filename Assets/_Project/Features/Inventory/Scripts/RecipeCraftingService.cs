using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Crafting service for CraftRecipeData-based recipes used by the current recipe UI.
    /// This service consumes materials only as inventory items; it does not trigger item use effects.
    /// </summary>
    public class RecipeCraftingService
    {
        private readonly InventoryService _inventoryService;

        public RecipeCraftingService(InventoryService inventoryService)
        {
            _inventoryService =
                inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        }

        public bool CanCraft(CraftRecipeData recipe, int quantity = 1)
        {
            return TryResolveMaterialStacks(recipe, quantity, out _);
        }

        public bool CanCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return CanUseSelectedMaterialStacks(recipe, materialA, materialB);
        }

        public int GetMaximumCraftable(CraftRecipeData recipe)
        {
            if (!TryGetValidMaterials(recipe, out var materials))
                return 0;

            int max = int.MaxValue;
            foreach (var material in materials)
            {
                int available = _inventoryService
                    .GetAllItems()
                    .Where(stack => CanUseAsMaterial(stack, material, 1))
                    .Sum(stack => stack.Count);
                max = Math.Min(max, available);
            }

            return Math.Max(0, max);
        }

        public bool TryCraft(CraftRecipeData recipe, int quantity)
        {
            if (!TryResolveMaterialStacks(recipe, quantity, out var consumptions))
                return false;

            if (!CanConsumeAll(consumptions))
                return false;

            foreach (var consumption in consumptions)
            {
                bool consumed = _inventoryService.ConsumeFromStack(
                    consumption.Stack,
                    consumption.Count
                );
                if (!consumed)
                    return false;
            }

            GrantResult(recipe, quantity);
            return true;
        }

        public bool TryCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            if (!CanUseSelectedMaterialStacks(recipe, materialA, materialB))
                return false;

            var consumptions = new List<StackConsumption> { new(materialA, 1), new(materialB, 1) };

            if (!CanConsumeAll(consumptions))
                return false;

            foreach (var consumption in consumptions)
            {
                bool consumed = _inventoryService.ConsumeFromStack(
                    consumption.Stack,
                    consumption.Count
                );
                if (!consumed)
                    return false;
            }

            GrantResult(recipe, 1);
            return true;
        }

        /// <summary>
        /// 結果アイテムを付与する。装備品は「端末で個体差ロール」した個体を quantity 個ぶん作る。
        /// 食材など非装備品は
        /// 固定ルールなのでそのまま数量ぶん追加する。単発 TryCraft の一部=確定でありプレビュー/再ロールは無い。
        /// </summary>
        private void GrantResult(CraftRecipeData recipe, int quantity)
        {
            if (recipe.resultItem is EquipmentData)
            {
                var a = recipe.material1 as EquipmentData;
                var b = recipe.material2 as EquipmentData;
                var rng = new SystemRandomSource();
                for (int i = 0; i < quantity; i++)
                {
                    var rolled = RollCraftedStats(
                        EquipmentData.ToStatVector(a),
                        EquipmentData.ToStatVector(b),
                        rng
                    );
                    _inventoryService.AddInstance(recipe.resultItem, RolledStat.FromVector(rolled));
                }
            }
            else
            {
                _inventoryService.AddItem(recipe.resultItem, quantity);
            }
        }

        private bool TryResolveMaterialStacks(
            CraftRecipeData recipe,
            int quantity,
            out List<StackConsumption> consumptions
        )
        {
            consumptions = null;

            if (recipe == null || recipe.resultItem == null || quantity <= 0)
                return false;

            if (!TryGetValidMaterials(recipe, out var materials))
                return false;

            consumptions = new List<StackConsumption>();
            var availableStacks = _inventoryService.GetAllItems();

            foreach (var material in materials)
            {
                int remaining = quantity;
                foreach (
                    var stack in availableStacks.Where(candidate =>
                        CanUseAsMaterial(candidate, material, 1)
                    )
                )
                {
                    int consumeCount = Math.Min(stack.Count, remaining);
                    consumptions.Add(new StackConsumption(stack, consumeCount));
                    remaining -= consumeCount;

                    if (remaining <= 0)
                        break;
                }

                if (remaining > 0)
                    return false;
            }

            return consumptions.Count > 0;
        }

        private bool CanUseAsMaterial(ItemStack stack, ItemData requiredItem, int count)
        {
            if (
                stack == null
                || stack.Data != requiredItem
                || stack.Count <= 0
                || stack.IsEquipped
                || _inventoryService.IsInQuickFood(stack)
            )
                return false;

            return stack.Count >= count;
        }

        private bool CanUseSelectedMaterialStacks(
            CraftRecipeData recipe,
            ItemStack materialA,
            ItemStack materialB
        )
        {
            if (!TryGetValidMaterials(recipe, out _))
                return false;

            if (
                !CanUseAsSelectedMaterial(materialA)
                || !CanUseAsSelectedMaterial(materialB)
                || materialA == materialB
                || materialA.Data == materialB.Data
            )
                return false;

            return recipe.MatchesMaterials(materialA.Data, materialB.Data);
        }

        private bool CanUseAsSelectedMaterial(ItemStack stack)
        {
            return stack != null
                && stack.Data != null
                && stack.Count > 0
                && !stack.IsEquipped
                && !_inventoryService.IsInQuickFood(stack);
        }

        private static bool TryGetValidMaterials(
            CraftRecipeData recipe,
            out List<ItemData> materials
        )
        {
            materials = null;

            if (recipe == null || recipe.resultItem == null)
                return false;

            materials = recipe.Materials.ToList();
            if (materials.Count != 2 || materials.Any(material => material == null))
                return false;

            // 調合は「装備品同士 / 食材同士」のみ(武器・大事なもの・カテゴリ跨ぎは不可)。
            // UI 非経由の直呼びも弾くためサービス層で明示ガードする。
            if (materials[0] is WeaponData || materials[1] is WeaponData)
                return false;
            if (materials[0].category != materials[1].category)
                return false;
            if (
                materials[0].category != ItemCategory.Equipment
                && materials[0].category != ItemCategory.Food
            )
                return false;

            return materials[0] != materials[1];
        }

        private bool CanConsumeAll(List<StackConsumption> consumptions)
        {
            return consumptions != null
                && consumptions.All(consumption =>
                    CanUseAsSelectedMaterial(consumption.Stack)
                    && _inventoryService.ContainsStack(consumption.Stack)
                    && consumption.Stack.Count >= consumption.Count
                );
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

        private readonly struct StackConsumption
        {
            public StackConsumption(ItemStack stack, int count)
            {
                Stack = stack;
                Count = count;
            }

            public ItemStack Stack { get; }
            public int Count { get; }
        }
    }
}
