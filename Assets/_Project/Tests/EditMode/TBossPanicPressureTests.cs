using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// TutorialBoss の追い込み判断（段階・系統ごとの補正値、合図のイベント、歯止めの息継ぎ）の検証。
    /// 設定は初期値（設計段階3の値）を使う。
    /// </summary>
    public class TBossPanicPressureTests
    {
        private const float Step = 0.1f;

        private TBossPanicPressure _pressure;
        private int _startedCount;
        private int _endedCount;
        private PanicCategory _startedCategory;

        [SetUp]
        public void SetUp()
        {
            _pressure = new TBossPanicPressure(new TBossPanicPressureSettings());
            _startedCount = 0;
            _endedCount = 0;
            _pressure.PressureStarted += category =>
            {
                _startedCount++;
                _startedCategory = category;
            };
            _pressure.PressureEnded += () => _endedCount++;
        }

        private void TickFor(PanicLevel level, PanicCategory category, float seconds, bool isEngaged = true)
        {
            for (float t = 0f; t < seconds - 0.0001f; t += Step)
            {
                _pressure.Tick(level, category, isEngaged, Step);
            }
        }

        [Test]
        public void 平常の相手には何も補正しない()
        {
            TickFor(PanicLevel.Calm, PanicCategory.Avoidance, 1f);

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.None));
            Assert.That(_pressure.AttackIntervalScale, Is.EqualTo(1f));
            Assert.That(_pressure.AdjustDirectAssaultChance(0.35f), Is.EqualTo(0.35f));
            Assert.That(_pressure.SpecialWeightScale, Is.EqualTo(1f));
            Assert.That(_pressure.ClosesIn, Is.False);
            Assert.That(_pressure.LostSightDurationScale, Is.EqualTo(1f));
        }

        [Test]
        public void 動揺している相手には軽く攻めを強める()
        {
            TickFor(PanicLevel.Tense, PanicCategory.Avoidance, 1f);

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Tense));
            Assert.That(_pressure.AttackIntervalScale, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(_pressure.AdjustDirectAssaultChance(0.35f), Is.EqualTo(0.5f).Within(0.001f));
            // 系統ごとの追い込みは焦りになってから
            Assert.That(_pressure.SpecialWeightScale, Is.EqualTo(1f));
            Assert.That(_startedCount, Is.EqualTo(0));
        }

        [Test]
        public void 焦った相手には系統ごとの追い込みを始めて合図を1回だけ出す()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Avoidance, 1f);

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Pressing));
            Assert.That(_startedCount, Is.EqualTo(1));
            Assert.That(_startedCategory, Is.EqualTo(PanicCategory.Avoidance));
            // 逃げ腰: 触手で引き戻し、突っ込み、しつこく追う
            Assert.That(_pressure.SpecialWeightScale, Is.EqualTo(3f));
            Assert.That(_pressure.AdjustDirectAssaultChance(0.35f), Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(_pressure.LostSightDurationScale, Is.EqualTo(1.5f));
            Assert.That(_pressure.ClosesIn, Is.False);
        }

        [Test]
        public void 合図の直後は攻撃を控える間がありその後は解ける()
        {
            _pressure.Tick(PanicLevel.Panic, PanicCategory.Avoidance, true, Step);
            Assert.That(_pressure.IsInCueLead, Is.True);

            TickFor(PanicLevel.Panic, PanicCategory.Avoidance, 0.6f);
            Assert.That(_pressure.IsInCueLead, Is.False);
        }

        [Test]
        public void 元の突っ込む確率の方が高ければ下げない()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Avoidance, 0.5f);

            Assert.That(_pressure.AdjustDirectAssaultChance(0.95f), Is.EqualTo(0.95f).Within(0.001f));
        }

        [Test]
        public void 交戦していなければ焦っていても追い込まない()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Freeze, 1f, isEngaged: false);

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.None));
            Assert.That(_startedCount, Is.EqualTo(0));
        }

        [Test]
        public void 最大時間を超えたら息継ぎに入り明けたら合図を出し直して再開する()
        {
            // 0.1秒刻みの誤差で境目が1ステップずれても判定できるよう、息継ぎに入るまで回して時刻を確かめる
            float elapsed = 0f;
            while (_pressure.Mode != TBossPressureMode.Breather && elapsed < 20f)
            {
                _pressure.Tick(PanicLevel.Panic, PanicCategory.Freeze, true, Step);
                elapsed += Step;
            }

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Breather));
            Assert.That(elapsed, Is.EqualTo(8.1f).Within(0.15f)); // 開始の1ステップ + 最大8秒
            Assert.That(_endedCount, Is.EqualTo(1));
            // 息継ぎ中は追い込まず、攻撃間隔も少し長めにする
            Assert.That(_pressure.AttackIntervalScale, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(_pressure.NormalWeightScale, Is.EqualTo(1f));
            Assert.That(_pressure.AdjustDirectAssaultChance(0.35f), Is.EqualTo(0.35f));

            // 息継ぎ4秒の間は焦っていても追い込みを再開しない
            TickFor(PanicLevel.Panic, PanicCategory.Freeze, 3.7f);
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Breather));

            // 息継ぎが明けたフレームの次から、その時の段階で判断し直す
            TickFor(PanicLevel.Panic, PanicCategory.Freeze, 0.6f);
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Pressing));
            Assert.That(_startedCount, Is.EqualTo(2));
        }

        [Test]
        public void 追い込み中にボスの攻撃が2回当たったら時間が残っていても息継ぎに入る()
        {
            TickFor(PanicLevel.Panic, PanicCategory.DefenseBreakdown, 1f);

            _pressure.NotifyBossHitPlayer();
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Pressing));

            _pressure.NotifyBossHitPlayer();
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Breather));
            Assert.That(_endedCount, Is.EqualTo(1));
        }

        [Test]
        public void 追い込み以外での命中は数えない()
        {
            TickFor(PanicLevel.Tense, PanicCategory.DefenseBreakdown, 0.5f);
            _pressure.NotifyBossHitPlayer();
            _pressure.NotifyBossHitPlayer();

            TickFor(PanicLevel.Panic, PanicCategory.DefenseBreakdown, 0.5f);
            _pressure.NotifyBossHitPlayer();

            // 追い込みに入ってからの1回だけなので、まだ息継ぎに入らない
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Pressing));
        }

        [Test]
        public void 相手が落ち着いたら息継ぎを挟まず今の段階に戻る()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Indecision, 1f);
            TickFor(PanicLevel.Tense, PanicCategory.Indecision, 0.1f);

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Tense));
            Assert.That(_endedCount, Is.EqualTo(1));

            // すぐにまた焦れば、合図を出して追い込み直す
            TickFor(PanicLevel.Panic, PanicCategory.Indecision, 0.1f);
            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.Pressing));
            Assert.That(_startedCount, Is.EqualTo(2));
        }

        [Test]
        public void 追い込みの途中で系統が変わったら追い込み方を合わせ合図は出し直さない()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Avoidance, 1f);
            TickFor(PanicLevel.Panic, PanicCategory.Indecision, 0.1f);

            Assert.That(_pressure.Category, Is.EqualTo(PanicCategory.Indecision));
            Assert.That(_pressure.ClosesIn, Is.True);
            Assert.That(_startedCount, Is.EqualTo(1));
        }

        [Test]
        public void 打ち切ると追い込みを終えて初期状態に戻る()
        {
            TickFor(PanicLevel.Panic, PanicCategory.Freeze, 1f);

            _pressure.Reset();

            Assert.That(_pressure.Mode, Is.EqualTo(TBossPressureMode.None));
            Assert.That(_endedCount, Is.EqualTo(1));
        }
    }
}
