using System;

namespace CreativeAI.StatRoll
{
    /// <summary>
    /// System.Random ベースの乱数源。シードを与えれば再現可能。
    /// 正規分布は Box-Muller 法で生成する。
    /// </summary>
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
