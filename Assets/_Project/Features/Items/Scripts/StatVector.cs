using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    // 装備品の能力値の型・値・装備補正をまとめる。
    // 抽選は DropStatRoller(入手時)と CraftingService(調合時)にある。

    /// <summary>
    /// 付与ステータスの型。装備品/武器は MaxHpPct、食材は HealAmount を持つ(排他)。
    /// </summary>
    public enum StatType
    {
        AttackPct, // 攻撃%
        DefensePct, // 防御%
        CritDamage, // 会心ダメージ
        CritRate, // 会心率
        MaxHpPct, // 最大HP%(装備品・武器)
        HealAmount, // HP即時回復(食材)
    }

    /// <summary>
    /// ステータス型 → 値 の疎なベクトル。値が正の型だけを保持する不変オブジェクト。
    /// </summary>
    public sealed class StatVector
    {
        private readonly Dictionary<StatType, float> _values;

        public static readonly StatVector Empty = new StatVector(new Dictionary<StatType, float>());

        public StatVector(IReadOnlyDictionary<StatType, float> values)
        {
            _values = new Dictionary<StatType, float>();
            foreach (var kv in values)
            {
                if (kv.Value > 0f)
                    _values[kv.Key] = kv.Value;
            }
        }

        /// <summary>未保持の型は 0 を返す。</summary>
        public float this[StatType type] => _values.TryGetValue(type, out var v) ? v : 0f;

        public IReadOnlyCollection<StatType> Types => _values.Keys;

        public int Count => _values.Count;

        /// <summary>総パワー = 全ステータス値の和。</summary>
        public float Power => _values.Values.Sum();

        public IReadOnlyDictionary<StatType, float> AsDictionary() => _values;

        public static StatVector Of(params (StatType type, float value)[] entries)
        {
            var dict = new Dictionary<StatType, float>();
            foreach (var (type, value) in entries)
                dict[type] = value;
            return new StatVector(dict);
        }

        public override string ToString()
        {
            if (_values.Count == 0)
                return "StatVector()";
            var body = string.Join(
                ", ",
                _values.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value:0.##}")
            );
            return $"StatVector({body})";
        }
    }

    /// <summary>
    /// 調合・拾得でロールされた個体ステータス1つ。stat は <see cref="StatType"/> 名(例: "AttackPct")で、
    /// 読み取りは大文字小文字を無視する。
    /// </summary>
    [Serializable]
    public sealed class RolledStat
    {
        public string stat;
        public float value;

        public RolledStat() { }

        public RolledStat(string stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }

        public static IReadOnlyList<RolledStat> FromVector(StatVector rolled)
        {
            var list = new List<RolledStat>(rolled.Count);
            foreach (var type in rolled.Types)
                list.Add(new RolledStat(type.ToString(), rolled[type]));
            return list;
        }

        /// <summary>ロール済みの能力値を抽選用のベクトルに戻す。未知の型名は無視する。</summary>
        public static StatVector ToVector(IReadOnlyList<RolledStat> rolled)
        {
            var values = new Dictionary<StatType, float>();
            if (rolled == null)
                return new StatVector(values);
            foreach (var r in rolled)
            {
                if (r != null && Enum.TryParse<StatType>(r.stat, true, out var type))
                    values[type] = values.GetValueOrDefault(type) + r.value;
            }
            return new StatVector(values);
        }
    }

    /// <summary>
    /// 装備(装備品 + 武器)による補正合計。InventoryManager / WeaponManager が積み上げ、PlayerStatus が素の値に合算する。
    /// 攻撃/防御/最大HP は 素の値×(1+割合%)、会心系は加算。移動・攻撃速度はコントローラ側なので持たない。
    /// </summary>
    public struct EquipmentBonus
    {
        // すべて %(パーセントポイント)。attackPct/defensePct/maxHpPct は素の値への割合で、
        // PlayerStatus が base×(1+Σ%/100) で適用する。criticalChance は会心率(%・加算)、
        // criticalDamage は会心ダメージ(%・会心時に攻撃力へ ÷100 で上乗せ)。
        public float attackPct;
        public float defensePct;
        public float maxHpPct;
        public float criticalChance;
        public float criticalDamage;

        /// <summary>指定した型の補正値を返す。装備補正に乗らない型(HealAmount)は 0。</summary>
        public readonly float Get(StatType type) =>
            type switch
            {
                StatType.AttackPct => attackPct,
                StatType.DefensePct => defensePct,
                StatType.MaxHpPct => maxHpPct,
                StatType.CritRate => criticalChance,
                StatType.CritDamage => criticalDamage,
                _ => 0f,
            };

        /// <summary>
        /// ロール済み個体ステータスを積み上げる。stat は StatType 名で、旧表記でも補正が消えないよう
        /// 大文字小文字を無視して照合する。未知の名前は無視する。
        /// </summary>
        public void Add(IReadOnlyList<RolledStat> rolled)
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
                        attackPct += r.value;
                        break;
                    case StatType.DefensePct:
                        defensePct += r.value;
                        break;
                    case StatType.MaxHpPct:
                        maxHpPct += r.value;
                        break;
                    case StatType.CritRate:
                        criticalChance += r.value;
                        break;
                    case StatType.CritDamage:
                        criticalDamage += r.value;
                        break;
                    // HealAmount は食材専用(装備補正には乗らない)。
                }
            }
        }
    }
}
