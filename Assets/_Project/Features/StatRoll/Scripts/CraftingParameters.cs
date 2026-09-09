using System.Collections.Generic;

namespace CreativeAI.StatRoll
{
    /// <summary>
    /// 調合アルゴリズムの調整ノブ(StatRollAlgorithm.md「パラメータ」表)。
    /// 値はバランス調整用。ここを差し替えるだけで挙動を変えられる。
    /// </summary>
    public sealed class CraftingParameters
    {
        /// <summary>β: ボーナス係数。弱い方の素材が上乗せする成長量。</summary>
        public double Beta { get; init; } = 0.5;

        /// <summary>C_cap: 総パワー上限。連鎖インフレを止める天井。</summary>
        public double PowerCap { get; init; } = 100.0;

        /// <summary>σ: シナジー強度。両親が共通で持つ型の増幅率。</summary>
        public double Synergy { get; init; } = 0.5;

        /// <summary>α0: 集中度。小さいほど尖り(当たり/はずれ)、大きいほど均等。</summary>
        public double Alpha0 { get; init; } = 6.0;

        /// <summary>ε: 切り捨て下限。これ未満の型は捨てて実質1型扱いにする。</summary>
        public double Epsilon { get; init; } = 1.0;

        /// <summary>付与数の上限(documents/Specification.md §1.1「アイテムカテゴリと付与ステータス」より固定で2)。</summary>
        public int MaxStatCount => 2;

        /// <summary>型ごとの値の上限(例: 会心率 ≤ 100%)。未登録の型は無制限。</summary>
        public IReadOnlyDictionary<StatType, float> Caps { get; init; } =
            new Dictionary<StatType, float> { { StatType.CritRate, 100f } };

        public static CraftingParameters Default => new CraftingParameters();
    }
}
