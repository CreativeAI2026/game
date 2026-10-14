using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    // 装備品の能力値(型・値・装備補正)と、入手時の抽選、抽選(調合・入手の両方)で共通に使う型をまとめる。
    // 調合だけの抽選とその調整値・確率分布は CraftingService にある。

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

    /// <summary>乱数源。テストでは固定値の実装を差し込み、抽選結果を決定的に検証する。</summary>
    public interface IRandomSource
    {
        /// <summary>[0,1) の一様乱数。</summary>
        double NextDouble();

        /// <summary>標準正規分布 N(0,1) の乱数(ガンマ標本生成に使う)。</summary>
        double NextGaussian();
    }

    /// <summary>System.Random ベースの乱数源。シードを与えれば再現可能。正規分布は Box-Muller 法。</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;
        private double _spareGaussian;
        private bool _hasSpare;

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public SystemRandomSource()
        {
            _random = new Random();
        }

        public double NextDouble() => _random.NextDouble();

        public double NextGaussian()
        {
            // Box-Muller は1回の計算で2つの標本を生む。片方を温存する。
            if (_hasSpare)
            {
                _hasSpare = false;
                return _spareGaussian;
            }

            double u1,
                u2;
            do
            {
                u1 = _random.NextDouble();
            } while (u1 <= double.Epsilon); // log(0) を避ける
            u2 = _random.NextDouble();

            double mag = Math.Sqrt(-2.0 * Math.Log(u1));
            _spareGaussian = mag * Math.Sin(2.0 * Math.PI * u2);
            _hasSpare = true;
            return mag * Math.Cos(2.0 * Math.PI * u2);
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
