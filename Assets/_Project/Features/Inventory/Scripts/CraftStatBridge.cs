using System;
using System.Collections.Generic;
using CreativeAI.StatRoll;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 調合ロールエンジン(CreativeAI.StatRoll)と装備品(EquipmentData / RolledStat)の橋渡し。食材は通さない。
    /// RolledStat.stat は <see cref="StatType"/> 名。型・上限・クランプなどはロール側が担う。
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
        ) => ToRolledStats(Roller.Roll(ToStatVector(a), ToStatVector(b), rng));

        /// <summary>
        /// フィールドドロップの装備品1個ぶんの個体ステータスをロールする。
        /// シード SO の固定値は「総パワー(強さの目安)」としてだけ使い、どの型に何ポイント付くかは
        /// 型抽選 + ディリクレ配分で拾った瞬間に決める(同じ場所の同じ装備品でも個体差が出る)。
        /// </summary>
        public static IReadOnlyList<RolledStat> RollDrop(EquipmentData seed, IRandomSource rng) =>
            ToRolledStats(DropStatRoller.Roll(ToStatVector(seed).Power, rng));

        private static IReadOnlyList<RolledStat> ToRolledStats(StatVector rolled)
        {
            var list = new List<RolledStat>(rolled.Count);
            foreach (var type in rolled.Types)
                list.Add(new RolledStat(type.ToString(), rolled[type]));
            return list;
        }

        /// <summary>
        /// ロール済み個体ステータスを装備補正に積み上げる。<see cref="RolledStat.stat"/> は <see cref="StatType"/> 名で、
        /// 旧表記でも補正が消えないよう大文字小文字を無視して照合する。未知の名前は無視する。
        /// </summary>
        public static void Accumulate(ref EquipmentBonus bonus, IReadOnlyList<RolledStat> rolled)
        {
            if (rolled == null)
                return;
            foreach (var r in rolled)
            {
                if (r == null || string.IsNullOrEmpty(r.stat))
                    continue;
                if (!Enum.TryParse<StatType>(r.stat, ignoreCase: true, out var type))
                    continue;

                switch (type)
                {
                    case StatType.AttackPct:
                        bonus.attackPct += r.value;
                        break;
                    case StatType.DefensePct:
                        bonus.defensePct += r.value;
                        break;
                    case StatType.MaxHpPct:
                        bonus.maxHpPct += r.value;
                        break;
                    case StatType.CritRate:
                        bonus.criticalChance += r.value;
                        break;
                    case StatType.CritDamage:
                        bonus.criticalDamage += r.value;
                        break;
                    // HealAmount は食材専用(装備補正には乗らない)。
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
