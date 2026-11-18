using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 焦り度の合算式（soft-OR・系統ごとの焦り度・指数追従）と、段階判定（ヒステリシス・最低保持時間）の検証。
    /// </summary>
    public class PanicScoreTests
    {
        // 目標値へ十分に追いつかせるための更新回数と1回の経過時間（下がる時の追従時間 4秒 の数倍）
        private const int SettleSteps = 300;
        private const float StepSeconds = 0.1f;

        /// <summary>スコアを直接決められる指標。</summary>
        private class FakeSignal : PanicSignal
        {
            private readonly PanicCategory _category;

            public FakeSignal(float weight, PanicCategory category)
                : base(weight)
            {
                _category = category;
            }

            public float CurrentScore { get; set; }

            public override PanicCategory Category => _category;
            public override string DisplayName => "テスト";
            public override float Score => CurrentScore;
            public override float RawValue => CurrentScore;
            public override string RawValueUnit => "値";

            public override void Tick(PanicContext context, float deltaTime) { }

            public override void ResetState() => CurrentScore = 0f;
        }

        private static void Settle(
            PanicScoreCalculator calculator,
            IReadOnlyList<PanicSignal> signals
        )
        {
            for (int i = 0; i < SettleSteps; i++)
            {
                calculator.Update(signals, StepSeconds);
            }
        }

        [Test]
        public void 指標1つが満点なら焦り度は重みの100倍になる()
        {
            var flight = new FakeSignal(0.7f, PanicCategory.Avoidance) { CurrentScore = 1f };
            var calculator = new PanicScoreCalculator();

            Settle(calculator, new PanicSignal[] { flight });

            Assert.That(calculator.Score, Is.EqualTo(70f).Within(0.1f));
        }

        [Test]
        public void 複数の指標がそろうと高くなるが100を超えない()
        {
            var flight = new FakeSignal(0.7f, PanicCategory.Avoidance) { CurrentScore = 1f };
            var spam = new FakeSignal(0.5f, PanicCategory.DefenseBreakdown) { CurrentScore = 1f };
            var calculator = new PanicScoreCalculator();

            Settle(calculator, new PanicSignal[] { flight, spam });

            // 1 − (1 − 0.7)(1 − 0.5) = 0.85
            Assert.That(calculator.Score, Is.EqualTo(85f).Within(0.1f));
        }

        [Test]
        public void 系統ごとの焦り度はその系統の指標だけで決まる()
        {
            var flight = new FakeSignal(0.7f, PanicCategory.Avoidance) { CurrentScore = 1f };
            var aimCancel = new FakeSignal(0.5f, PanicCategory.Indecision) { CurrentScore = 0.4f };
            var calculator = new PanicScoreCalculator();

            Settle(calculator, new PanicSignal[] { flight, aimCancel });

            Assert.That(
                calculator.GetCategoryScore(PanicCategory.Avoidance),
                Is.EqualTo(70f).Within(0.1f)
            );
            Assert.That(
                calculator.GetCategoryScore(PanicCategory.Indecision),
                Is.EqualTo(20f).Within(0.1f)
            );
            Assert.That(
                calculator.GetCategoryScore(PanicCategory.DefenseBreakdown),
                Is.EqualTo(0f).Within(0.01f)
            );
            Assert.That(calculator.DominantCategory, Is.EqualTo(PanicCategory.Avoidance));
        }

        [Test]
        public void 上がる時は速く下がる時は遅く追従する()
        {
            var flight = new FakeSignal(1f, PanicCategory.Avoidance) { CurrentScore = 1f };
            var signals = new PanicSignal[] { flight };
            var calculator = new PanicScoreCalculator();

            // 上がる時の追従時間（0.3秒）で目標 100 の約63%
            calculator.Update(signals, 0.3f);
            Assert.That(calculator.Score, Is.EqualTo(63.2f).Within(0.5f));

            Settle(calculator, signals);
            flight.CurrentScore = 0f;

            // 同じ0.3秒でも、下がる時（追従時間4秒）は約7%しか下がらない
            calculator.Update(signals, 0.3f);
            Assert.That(calculator.Score, Is.EqualTo(92.8f).Within(0.5f));
        }

        [Test]
        public void 段階は上がる境目と下がる境目がずれている()
        {
            var tracker = new PanicLevelTracker();
            // EditMode では Time.time が進まないため、最低保持時間を切って境目だけを検証する
            TestReflection.SetField(tracker, "_minHoldSeconds", 0f);

            Assert.That(tracker.Update(29f), Is.False);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Calm));

            Assert.That(tracker.Update(30f), Is.True);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Tense));

            // 上がる境目(30)を下回っても、下がる境目(20)までは動揺のまま
            tracker.Update(25f);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Tense));

            tracker.Update(60f);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Panic));

            // 上がる境目(60)を下回っても、下がる境目(45)までは焦りのまま
            tracker.Update(50f);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Panic));

            tracker.Update(40f);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Tense));

            tracker.Update(19f);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Calm));
        }

        [Test]
        public void 段階は最低保持時間の間は下がらないが上がるのは即座()
        {
            var tracker = new PanicLevelTracker();

            // 平常から一気に焦りへは、保持時間に関係なく上がる
            Assert.That(tracker.Update(70f), Is.True);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Panic));

            // EditMode では Time.time が進まないので、保持時間（1.5秒）が経たず下がらない
            Assert.That(tracker.Update(0f), Is.False);
            Assert.That(tracker.Level, Is.EqualTo(PanicLevel.Panic));
        }
    }
}
