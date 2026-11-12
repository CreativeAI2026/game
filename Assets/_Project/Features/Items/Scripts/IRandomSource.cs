using System;

namespace CreativeAI.Gameplay
{
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
}
