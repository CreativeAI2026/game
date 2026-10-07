using System;
using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    // 装備品の能力値(型・値・装備補正)と、その抽選(調合・フィールド拾得の両方)で共通に使う型をまとめる。
    // 調合だけの抽選は RecipeCraftingService、拾得だけの抽選は FieldItemPickup にある。

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
    /// 能力値抽選の調整ノブ(調合・拾得で共用)。値はバランス調整用。
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
