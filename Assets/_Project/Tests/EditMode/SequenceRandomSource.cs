using System.Collections.Generic;
using CreativeAI.StatRoll;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 与えた数列を順に返すテスト用乱数源。尽きたら最後の値を返し続ける。
    /// ロール結果を決め打ちで検証したいときに使う。
    /// </summary>
    public sealed class SequenceRandomSource : IRandomSource
    {
        private readonly Queue<double> _uniform;
        private readonly Queue<double> _gaussian;
        private double _lastUniform;
        private double _lastGaussian;

        public SequenceRandomSource(double[] uniform = null, double[] gaussian = null)
        {
            _uniform = new Queue<double>(uniform ?? new[] { 0.5 });
            _gaussian = new Queue<double>(gaussian ?? new[] { 0.0 });
        }

        public double NextDouble()
        {
            if (_uniform.Count > 0)
                _lastUniform = _uniform.Dequeue();
            return _lastUniform;
        }

        public double NextGaussian()
        {
            if (_gaussian.Count > 0)
                _lastGaussian = _gaussian.Dequeue();
            return _lastGaussian;
        }
    }
}
