using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.StatRoll
{
    /// <summary>
    /// ステータス型 → 値 の疎なベクトル。値が正の型だけを保持する。
    /// 調合の素材・結果の両方を表す不変オブジェクト。
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

        /// <summary>総パワー = 全ステータス値の和(StatRollAlgorithm.md 層1)。</summary>
        public float Power => _values.Values.Sum();

        public IReadOnlyDictionary<StatType, float> AsDictionary() => _values;

        /// <summary>可変辞書から組み立てるビルダ的ファクトリ。</summary>
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
}
