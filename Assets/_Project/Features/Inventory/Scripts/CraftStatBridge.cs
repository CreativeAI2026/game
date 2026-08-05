using System.Collections.Generic;
using CreativeAI.Crafting;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 調合ロールエンジン(CreativeAI.Crafting)とインベントリの装備品(EquipmentData / RolledStat)の橋渡し。
    /// 装備品同士の調合は「端末で個体差ロール」する(documents/Specification.md §2.3,
    /// CraftingStatAlgorithm.md)。食材は固定ルール(FoodData.HealFraction)なのでここは通さない。
    ///
    /// RolledStat.stat の語彙は <see cref="StatType"/> 名(例 "AttackPct")。GetEquippedBonus はこの語彙で解釈する。
    /// 付与ステータスの型・上限2つ・会心率クランプなどは全てロール側(CraftingStatRoller/CraftingParameters)が担う。
    /// </summary>
    public static class CraftStatBridge
    {
        private static readonly CraftingStatRoller Roller = new CraftingStatRoller();

        /// <summary>
        /// 装備品2つから結果の個体ステータスをロールする。素材の固定ステータスを StatVector に写して
        /// ロールし、結果を RolledStat 列(付与数は最大2)に変換して返す。
        /// </summary>
        public static IReadOnlyList<RolledStat> RollEquipment(
            EquipmentData a,
            EquipmentData b,
            IRandomSource rng
        )
        {
            var rolled = Roller.Roll(ToStatVector(a), ToStatVector(b), rng);
            var list = new List<RolledStat>(rolled.Count);
            foreach (var type in rolled.Types)
                list.Add(new RolledStat(type.ToString(), rolled[type]));
            return list;
        }

        /// <summary>ロール済み個体ステータスを装備補正に積み上げる(GetEquippedBonus 用)。</summary>
        public static void Accumulate(ref EquipmentBonus bonus, IReadOnlyList<RolledStat> rolled)
        {
            if (rolled == null)
                return;
            foreach (var r in rolled)
            {
                if (r == null)
                    continue;
                switch (r.stat)
                {
                    case nameof(StatType.AttackPct):
                        bonus.attack += r.value;
                        break;
                    case nameof(StatType.DefensePct):
                        bonus.defense += r.value;
                        break;
                    case nameof(StatType.MaxHpPct):
                        bonus.maxHp += r.value;
                        break;
                    case nameof(StatType.CritRate):
                        bonus.criticalChance += r.value;
                        break;
                    case nameof(StatType.CritDamage):
                        bonus.criticalDamage += r.value;
                        break;
                }
            }
        }

        private static StatVector ToStatVector(EquipmentData e)
        {
            if (e == null)
                return StatVector.Empty;
            // 0 以下の型は StatVector 側で自動的に落ちる。
            return StatVector.Of(
                (StatType.AttackPct, e.attack),
                (StatType.DefensePct, e.defense),
                (StatType.CritDamage, e.criticalDamage),
                (StatType.CritRate, e.criticalRate),
                (StatType.MaxHpPct, e.maxHP)
            );
        }
    }
}
