using System.Linq;
using CreativeAI.Crafting;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class ProbabilityDistributionsTests
    {
        [Test]
        public void SampleDirichlet_SumsToOne_AndNonNegative()
        {
            var rng = new SystemRandomSource(seed: 12345);
            var p = ProbabilityDistributions.SampleDirichlet(new[] { 2.0, 3.0, 1.0 }, rng);

            Assert.AreEqual(3, p.Length);
            Assert.AreEqual(1.0, p.Sum(), 1e-9);
            foreach (var v in p)
                Assert.GreaterOrEqual(v, 0.0);
        }

        [Test]
        public void SampleDirichlet_IsDeterministic_ForSameSeed()
        {
            var a = ProbabilityDistributions.SampleDirichlet(
                new[] { 2.0, 3.0 },
                new SystemRandomSource(7)
            );
            var b = ProbabilityDistributions.SampleDirichlet(
                new[] { 2.0, 3.0 },
                new SystemRandomSource(7)
            );
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void SampleGamma_IsPositive()
        {
            var rng = new SystemRandomSource(99);
            for (int i = 0; i < 100; i++)
            {
                Assert.Greater(ProbabilityDistributions.SampleGamma(0.5, rng), 0.0);
                Assert.Greater(ProbabilityDistributions.SampleGamma(5.0, rng), 0.0);
            }
        }

        [Test]
        public void SampleGamma_MeanApproximatesShape()
        {
            // Gamma(shape, scale=1) の期待値は shape。大量標本の平均で確認。
            var rng = new SystemRandomSource(2024);
            const double shape = 4.0;
            const int n = 20000;
            double sum = 0.0;
            for (int i = 0; i < n; i++)
                sum += ProbabilityDistributions.SampleGamma(shape, rng);
            double mean = sum / n;
            Assert.AreEqual(shape, mean, 0.15); // 統計的な緩い許容
        }
    }
}
