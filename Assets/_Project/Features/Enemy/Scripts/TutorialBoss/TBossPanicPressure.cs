using System;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの焦りの段階・系統から「今どう追い込むか」を決め、ボスの各判断に掛ける補正値を返す。
    /// TBossPerception・TBossCombatTempo と同じく TutorialBossController が所有するプレーンなクラス。
    /// 各ステートは補正値を掛けるだけにし、追い込み方の判断はここに集める。
    ///
    /// 状態の移り変わり:
    ///   None ⇔ Tense（相手の段階が 平常 ⇔ 動揺）
    ///   None/Tense → Pressing（相手が焦り）… 開始の合図として <see cref="PressureStarted"/> を出す
    ///   Pressing → Breather（最大時間を超えた・命中数が上限に達した）… 一定時間は追い込まない
    ///   Pressing → None/Tense（相手が落ち着いた・交戦が切れた）
    ///   Breather → None（息継ぎが明けた。その時の段階で次のフレームから判断し直す）
    ///
    /// TutorialBoss は倒されるべき最初のボスなので、追い込みは「焦りを見せた隙を突く」までにとどめ、
    /// 最大時間と命中数の上限で必ず息継ぎを挟む（当たる→焦る→さらに攻める、のハメにしない）。
    /// 時間は Tick に渡された経過時間で数えるので、テストから時間を進められる。
    /// </summary>
    public class TBossPanicPressure
    {
        private readonly TBossPanicPressureSettings _settings;

        private float _pressureElapsed;
        private float _breatherRemaining;
        private int _hitsDuringPressure;

        public TBossPanicPressure(TBossPanicPressureSettings settings)
        {
            _settings = settings;
        }

        /// <summary>現在の追い込みの状態。</summary>
        public TBossPressureMode Mode { get; private set; } = TBossPressureMode.None;

        /// <summary>追い込み中の系統（Pressing の時だけ意味を持つ）。追い込みの途中で相手の系統が変われば追従する。</summary>
        public PanicCategory Category { get; private set; }

        /// <summary>追い込みを始めてからの経過時間（秒）。合図から最初の攻撃までの間を取るのに使う。</summary>
        public float PressureElapsed => _pressureElapsed;

        /// <summary>追い込みを続けられる残り時間（秒）。Pressing 以外は 0。</summary>
        public float PressureRemaining =>
            Mode == TBossPressureMode.Pressing
                ? Math.Max(0f, _settings.MaxPressureSeconds - _pressureElapsed)
                : 0f;

        /// <summary>息継ぎの残り時間（秒）。Breather 以外は 0。</summary>
        public float BreatherRemaining =>
            Mode == TBossPressureMode.Breather ? _breatherRemaining : 0f;

        /// <summary>追い込み開始の合図から、最初の追い込み攻撃までに最低限あける時間（秒）。</summary>
        public float CueLeadSeconds => _settings.CueLeadSeconds;

        /// <summary>
        /// 合図を出した直後で、まだ攻撃を仕掛けてはいけない間か。
        /// 合図と同時に殴られると、気付いた瞬間には遅く合図の意味が無いため、この間は突っ込みも止める。
        /// </summary>
        public bool IsInCueLead => IsPressing && _pressureElapsed < _settings.CueLeadSeconds;

        /// <summary>追い込みを始めた瞬間に発火する（合図の目の光・音を出すため）。引数は追い込む系統。</summary>
        public event Action<PanicCategory> PressureStarted;

        /// <summary>追い込みが終わった瞬間（息継ぎに入った・相手が落ち着いた）に発火する。</summary>
        public event Action PressureEnded;

        // --- 各判断に掛ける補正値 ---

        /// <summary>攻撃と攻撃の間隔に掛ける倍率。</summary>
        public float AttackIntervalScale =>
            Mode switch
            {
                TBossPressureMode.Tense => _settings.TenseAttackIntervalScale,
                TBossPressureMode.Pressing => CurrentProfile.AttackIntervalScale,
                TBossPressureMode.Breather => _settings.BreatherAttackIntervalScale,
                _ => 1f,
            };

        /// <summary>通常攻撃の選ばれやすさに掛ける倍率。</summary>
        public float NormalWeightScale => IsPressing ? CurrentProfile.NormalWeightScale : 1f;

        /// <summary>通常攻撃2（鎌）の選ばれやすさに掛ける倍率。</summary>
        public float Normal2WeightScale => IsPressing ? CurrentProfile.Normal2WeightScale : 1f;

        /// <summary>特殊攻撃（触手）の選ばれやすさに掛ける倍率。</summary>
        public float SpecialWeightScale => IsPressing ? CurrentProfile.SpecialWeightScale : 1f;

        /// <summary>様子見の間合いを内側まで詰めるか（弓を構えられても下がらない）。</summary>
        public bool ClosesIn => IsPressing && CurrentProfile.ClosesIn;

        /// <summary>プレイヤーを見失ってから諦めるまでの時間に掛ける倍率。</summary>
        public float LostSightDurationScale =>
            IsPressing ? CurrentProfile.LostSightDurationScale : 1f;

        private bool IsPressing => Mode == TBossPressureMode.Pressing;

        private TBossPressureProfile CurrentProfile => _settings.GetProfile(Category);

        /// <summary>
        /// 追跡で間合いに入った時に、様子見を挟まず突っ込む確率を補正する。
        /// 動揺には上乗せし、焦りには系統ごとの下限を使う（元の確率の方が高ければ元のまま）。
        /// </summary>
        public float AdjustDirectAssaultChance(float baseChance) =>
            Mode switch
            {
                TBossPressureMode.Tense => Math.Min(
                    1f,
                    baseChance + _settings.TenseDirectAssaultBonus
                ),
                TBossPressureMode.Pressing => Math.Max(
                    baseChance,
                    CurrentProfile.DirectAssaultChance
                ),
                _ => baseChance,
            };

        /// <summary>
        /// 1フレーム分の判断を更新する。
        /// </summary>
        /// <param name="level">プレイヤーの焦りの段階</param>
        /// <param name="dominantCategory">プレイヤーの焦りの系統（最も高いもの）</param>
        /// <param name="isEngaged">ボスとプレイヤーが交戦中か。交戦していなければ追い込まない</param>
        /// <param name="deltaTime">経過時間（秒）</param>
        public void Tick(
            PanicLevel level,
            PanicCategory dominantCategory,
            bool isEngaged,
            float deltaTime
        )
        {
            switch (Mode)
            {
                case TBossPressureMode.Breather:
                    _breatherRemaining -= deltaTime;
                    if (_breatherRemaining <= 0f)
                    {
                        _breatherRemaining = 0f;
                        Mode = TBossPressureMode.None;
                    }
                    return;

                case TBossPressureMode.Pressing:
                    TickPressing(level, dominantCategory, isEngaged, deltaTime);
                    return;

                default:
                    TickIdle(level, dominantCategory, isEngaged);
                    return;
            }
        }

        /// <summary>
        /// ボスの攻撃がプレイヤーに当たった時に呼ぶ。追い込み中なら命中数を数え、上限に達したら息継ぎに入る。
        /// </summary>
        public void NotifyBossHitPlayer()
        {
            if (!IsPressing)
                return;

            _hitsDuringPressure++;
            if (_hitsDuringPressure >= _settings.MaxHitsPerPressure)
            {
                StartBreather();
            }
        }

        /// <summary>交戦が終わった時などに、追い込みも息継ぎも打ち切って初期状態へ戻す。</summary>
        public void Reset()
        {
            bool wasPressing = IsPressing;
            Mode = TBossPressureMode.None;
            _pressureElapsed = 0f;
            _breatherRemaining = 0f;
            _hitsDuringPressure = 0;
            if (wasPressing)
            {
                PressureEnded?.Invoke();
            }
        }

        private void TickIdle(PanicLevel level, PanicCategory dominantCategory, bool isEngaged)
        {
            if (!isEngaged)
            {
                Mode = TBossPressureMode.None;
                return;
            }

            if (level == PanicLevel.Panic)
            {
                StartPressure(dominantCategory);
                return;
            }

            Mode = level == PanicLevel.Tense ? TBossPressureMode.Tense : TBossPressureMode.None;
        }

        private void TickPressing(
            PanicLevel level,
            PanicCategory dominantCategory,
            bool isEngaged,
            float deltaTime
        )
        {
            if (!isEngaged || level != PanicLevel.Panic)
            {
                // 相手が落ち着いた（または交戦が切れた）。息継ぎは挟まず、今の段階に合わせ直す
                Mode =
                    isEngaged && level == PanicLevel.Tense
                        ? TBossPressureMode.Tense
                        : TBossPressureMode.None;
                PressureEnded?.Invoke();
                return;
            }

            // 追い込みの途中で相手の焦り方が変わったら、追い込み方もそれに合わせる（合図は出し直さない）
            Category = dominantCategory;

            _pressureElapsed += deltaTime;
            if (_pressureElapsed >= _settings.MaxPressureSeconds)
            {
                StartBreather();
            }
        }

        private void StartPressure(PanicCategory category)
        {
            Mode = TBossPressureMode.Pressing;
            Category = category;
            _pressureElapsed = 0f;
            _hitsDuringPressure = 0;
            PressureStarted?.Invoke(category);
        }

        private void StartBreather()
        {
            Mode = TBossPressureMode.Breather;
            _breatherRemaining = _settings.BreatherSeconds;
            PressureEnded?.Invoke();
        }
    }
}
