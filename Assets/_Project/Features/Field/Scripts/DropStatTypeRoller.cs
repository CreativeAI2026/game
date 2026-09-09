using System;
using System.Collections.Generic;
using CreativeAI.StatRoll;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドに落ちている装備品(合成前のシード)の付与ステータス「型」を決める
    /// 重み付き非復元抽出(documents/Specification.md §2.1.1)。
    ///
    /// ここで決まるのは<b>どの型が付くか</b>だけ。付与「量」は
    /// <see cref="CraftingStatRoller"/> のロールモデルが決める(同 §2.1.1)。
    /// 対象は装備品のみ。食材は固定ルール(HP即時回復)なのでこの抽選を通さない。
    ///
    /// 呼び出し元は <see cref="DropStatRoller"/>(型 + 量をまとめてロールする)。
    /// 拾得の入口は <see cref="FieldItemPickup"/>。
    /// </summary>
    public static class DropStatTypeRoller
    {
        /// <summary>1アイテムの付与数上限(Specification.md §2.1「付与数 最大2つ」)。</summary>
        public const int MaxStatCount = 2;

        /// <summary>
        /// 型ごとの抽選重み(Specification.md §2.1.1 の表)。
        /// 攻撃% 2 / 防御% 2 / 最大HP% 2 / 会心ダメージ 1 / 会心率 1 = 合計8
        /// → 25% / 25% / 25% / 12.5% / 12.5%。
        /// 配列にしているのは列挙順を固定して抽選を再現可能にするため。
        /// 食材専用の <see cref="StatType.HealAmount"/> は装備品の候補に含めない。
        /// </summary>
        public static readonly IReadOnlyList<(StatType Type, int Weight)> Weights = new[]
        {
            (StatType.AttackPct, 2),
            (StatType.DefensePct, 2),
            (StatType.MaxHpPct, 2),
            (StatType.CritDamage, 1),
            (StatType.CritRate, 1),
        };

        /// <summary>
        /// 付与型を重み付き<b>非復元</b>抽出で選ぶ。1度選ばれた型は候補から外れるので同じ型は重複しない。
        /// </summary>
        /// <param name="random">乱数源。テストでは決定的な実装を差し込む。</param>
        /// <param name="count">
        /// 選ぶ型の数。0 未満は 0、<see cref="MaxStatCount"/> 超はそれに丸める。
        /// </param>
        /// <returns>選ばれた型(選ばれた順)。重複なし・最大 <see cref="MaxStatCount"/> 個。</returns>
        public static IReadOnlyList<StatType> Roll(IRandomSource random, int count = MaxStatCount)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int n = Math.Clamp(count, 0, MaxStatCount);
            var picked = new List<StatType>(n);
            if (n == 0)
                return picked;

            var remaining = new List<(StatType Type, int Weight)>(Weights);
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
