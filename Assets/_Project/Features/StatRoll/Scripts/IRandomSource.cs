namespace CreativeAI.StatRoll
{
    /// <summary>
    /// 乱数源の抽象。テストでは固定シード/固定値の実装を差し込み、
    /// ロール結果を決定的に検証できるようにする。
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>[0,1) の一様乱数。</summary>
        double NextDouble();

        /// <summary>標準正規分布 N(0,1) の乱数(ガンマ標本生成に使う)。</summary>
        double NextGaussian();
    }
}
