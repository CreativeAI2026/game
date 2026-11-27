using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボス専用のAIコントローラ。
    /// 子オブジェクトの懐中電灯（Spotlight）による視界判定と、
    /// NoiseEventBusを通じた音への反応を主軸とした行動制御を行う。
    /// </summary>
    public class TutorialBossController : EnemyBaseController
    {
        private static readonly int VelocityXHash = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZHash = Animator.StringToHash("VelocityZ");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

        // 同じ攻撃が何回連続で選ばれたら次は避けるか（3回目の選択をブロックする）。
        private const int MaxAttackRepeatBeforeAvoid = 2;

        // 「離れた」とみなすための、体が触れる距離に足す余裕（m）
        private const float SeparationMargin = 0.1f;

        // 走りモーションに切り替える／戻す速度を、歩き速度と走り速度の間のどこに置くか（0=歩き速度、1=走り速度）。
        // 入りと戻りの閾値をずらし、閾値付近の速度で走り⇔歩きがチラつくのを防ぐ。
        private const float RunAnimationEnterSpeedRatio = 0.6f;
        private const float RunAnimationExitSpeedRatio = 0.4f;

        // ステートが変わる時に捨てる、行動モーション用のトリガー（死亡は捨てない）
        private static readonly string[] ActionTriggers =
        {
            "Attack",
            "Attack2",
            "SpecialAttack",
            "Flinch",
            "Parried",
            "Look",
            "Repel",
        };

        [Header("懐中電灯（視界）設定")]
        [Tooltip("懐中電灯オブジェクトのTransform。回転制御に使用する。")]
        [SerializeField]
        private Transform flashlightTransform;

        [Tooltip("懐中電灯のLightコンポーネント。SpotAngleとRangeを視界範囲として使用する。")]
        [SerializeField]
        private Light flashlightLight;

        [Tooltip("プレイヤーを捕捉し続ける・見失うまでの設定。")]
        [SerializeField]
        private TBossSightSettings _sight = new();

        [Header("エフェクト")]
        [Tooltip("鎌のTrailEffect")]
        [SerializeField]
        private TrailRenderer scytheTrail;

        [Header("移動")]
        [SerializeField]
        private TBossMovementSettings _movement = new();

        [Header("交戦（間合い・攻撃の間隔）")]
        [SerializeField]
        private TBossEngagementSettings _engagement = new();

        [Header("通常攻撃（踏み込み）")]
        [SerializeField]
        private TBossNormalAttackSettings _normalAttack = new();

        [Tooltip(
            "Attack/Attack2共通で使う近接ヒットボックス。ヒット確定演出（引き寄せ）の判定にも使う。"
        )]
        [SerializeField]
        private EnemyMeleeHitbox meleeHitbox;

        [Header("通常攻撃2（鎌）")]
        [SerializeField]
        private TBossNormalAttack2Settings _normalAttack2 = new();

        [Tooltip("通常攻撃2で使う、伸びた腕全体の攻撃判定。")]
        [SerializeField]
        private BoneChainHitbox armSweepHitbox;

        [Tooltip("通常攻撃2で、地面に刺した鎌を引きずる時の火花。")]
        [SerializeField]
        private TBossGroundDragSparks groundDragSparks;

        [Header("特殊攻撃（ワイヤー）")]
        [SerializeField]
        private TBossSpecialAttackSettings _specialAttack = new();

        [Tooltip("ワイヤーとして伸ばすボーン。")]
        [SerializeField]
        private Transform wireBone;

        [Tooltip(
            "ワイヤーボーンの初期ローカル座標（リトラクト時に戻す位置）。実行時に自動取得されます。"
        )]
        [HideInInspector]
        public Vector3 InitialWireBoneLocalPosition;

        [Tooltip(
            "ワイヤーボーンの初期ローカル回転（リトラクト時に戻す姿勢）。実行時に自動取得されます。"
        )]
        [HideInInspector]
        public Quaternion InitialWireBoneLocalRotation = Quaternion.identity;

        [Header("しなやかな腕（ボーン連鎖）設定")]
        [Tooltip("特殊攻撃（ワイヤー）側の腕の連鎖。根元→先端の順に設定する。")]
        [SerializeField]
        private TBossLimbChainSettings wireChainSettings = new TBossLimbChainSettings();

        [Header("障害物設定")]
        [SerializeField]
        private LayerMask obstacleLayer;

        [Header("掴み（捕獲）")]
        [SerializeField]
        private TBossGrabSettings _grab = new();

        [Tooltip(
            "電撃エフェクトのプレハブ。ワイヤーの付け根からプレイヤーの首元までの間に電流を流す。"
        )]
        [SerializeField]
        private ElectricArcEffect electricEffectPrefab;

        [Tooltip("電撃ダメージフェーズ用カメラ(DamageSceneCamera)の追従ターゲット(GrabTarget)。")]
        [SerializeField]
        private Transform grabCameraTarget;

        [Header("焦りへの追い込み")]
        [Tooltip(
            "プレイヤーの焦り（PanicDetector）に応じて攻めを強める設定。段階・系統ごとの補正値と歯止め。"
        )]
        [SerializeField]
        private TBossPanicPressureSettings panicPressureSettings = new TBossPanicPressureSettings();

        //  内部状態（ステートから読み書き）
        /// <summary>最後に聴取した音源のワールド座標。SoundInvestigateStateの目標地点として使う。</summary>
        [HideInInspector]
        public Vector3 LastHeardSoundPosition;

        /// <summary>プレイヤーが光の外にいる継続時間のカウンタ。</summary>
        [HideInInspector]
        public float LostSightTimer;

        // 攻撃選択の内部状態。クールダウン管理と「同じ攻撃の連続選択を避ける」判定にのみ使うため非公開で保持する。
        private float _normalAttackReadyTime;
        private float _normalAttack2ReadyTime;
        private float _specialAttackReadyTime;
        private TBossAttackType? _lastAttackType;
        private int _lastAttackRepeatCount;

        // 交戦中の攻撃タイミング。追跡・様子見の往復で待ち時間がやり直しにならないよう、ステートではなくここで持つ
        private readonly TBossCombatTempo _combatTempo = new TBossCombatTempo();

        // 見渡しモーションの再生と、その間の足止め。Awake で生成する（Animator が必要なため）
        private TBossLookAround _lookAround;

        // 各ステートが要求している「NavMeshAgent を止めるか」。見渡し中はこれに関係なく止める
        private bool _agentStopRequested;

        private PlayerStatus _playerStatus;

        // プレイヤーの焦り度。無いプレイヤー（焦り度を持たないシーン）でもボスは追い込みなしで動く
        private PanicDetector _playerPanic;

        // 追い込み開始の合図（目の光）。無ければ音だけで合図する
        private TBossEyeGlint _eyeGlint;

        // プレイヤー死亡時の交戦終了を一度だけ行うためのフラグ（復活した場合は解除する）
        private bool _hasDisengagedFromDeadPlayer;

        // 今の掴みを、追い込みの歯止めの命中として数え済みか（掴みが終わったら戻す）
        private bool _hasCountedCurrentCapture;

        // 今のステートの1つ前のステート。様子見に入った時に、攻撃の後か・防御の後かを見分けるのに使う
        private TBossBaseState _previousState;

        // プレイヤーの攻撃ボタンの入力。押された瞬間に反応して防御するのに使う
        private PlayerInputHandler _playerInput;

        // 遠距離の防御: 弓を構え始めた瞬間を見るための前フレームの値と、次に確率を引ける時刻
        private PlayerController _playerController;
        private bool _wasPlayerAiming;
        private float _nextRangedGuardRollTime;

        // プレイヤーとの衝突を戻す予約。重なったまま戻すと、プレイヤーがめり込みから押し出されて上へ飛ぶため、離れるまで待つ
        private bool _isPlayerCollisionRestorePending;

        private bool _isRunAnimationActive;

        // 外部参照用
        public Transform FlashlightTransform => flashlightTransform;
        public Light FlashlightLight => flashlightLight;

        /// <summary>プレイヤーを捕捉し続ける・見失うまでの設定。</summary>
        public TBossSightSettings Sight => _sight;

        /// <summary>移動の速さの設定。</summary>
        public TBossMovementSettings Movement => _movement;

        /// <summary>交戦中の間合いと攻撃の間隔の設定。</summary>
        public TBossEngagementSettings Engagement => _engagement;

        /// <summary>通常攻撃（踏み込み）の設定。</summary>
        public TBossNormalAttackSettings NormalAttack => _normalAttack;

        /// <summary>通常攻撃2（鎌）の設定。</summary>
        public TBossNormalAttack2Settings NormalAttack2 => _normalAttack2;

        /// <summary>特殊攻撃（ワイヤー）の設定。</summary>
        public TBossSpecialAttackSettings SpecialAttack => _specialAttack;

        /// <summary>掴み（捕獲）の設定。</summary>
        public TBossGrabSettings Grab => _grab;

        /// <summary>プレイヤーが光から外れてから見失うまでの時間（秒）。焦りへの追い込み（逃げ腰）で長くなる。</summary>
        public float EffectiveLostSightDuration =>
            _sight.LostSightDuration * PanicPressure.LostSightDurationScale;

        /// <summary>
        /// ボス本体の拡大率。ステート内の固定距離（到着判定・徘徊半径など）はスケール1の体格で調整した値なので、
        /// これを掛けて体格に合わせる。Prefabのスケール変更で行動が破綻しないようにするため。
        /// </summary>
        public float BodyScale => transform.lossyScale.y;

        /// <summary>
        /// 追跡で間合いに入った時、様子見を挟まず突っ込む確率（焦りへの追い込みで補正済み）。
        /// 追い込み開始の合図の直後は、プレイヤーが身構える間を取るため突っ込まない。
        /// </summary>
        public float EffectiveDirectAssaultChance =>
            PanicPressure.IsInCueLead
                ? 0f
                : PanicPressure.AdjustDirectAssaultChance(_engagement.DirectAssaultChance);

        public Transform WireBone => wireBone;

        /// <summary>
        /// 音が聞こえたかと、その確信度（0〜1）を判定する。聞こえる範囲は鳴らした側が決めた音の半径（足音・ハンマーなど）で、
        /// 音源がその半径の端なら確信度0、すぐそばなら1。大きな音ほど遠くから聞こえ、遠くでも確信度が残る。
        /// 以前はボス側の聴覚半径でも上限を掛けていたため、音の半径を広げても一定距離より遠いと聞こえなかった。
        /// </summary>
        public bool TryHearSound(NoiseEvent data, out float confidence)
        {
            confidence = 0f;
            if (data.Radius <= 0f)
            {
                return false;
            }

            float distToSound = Vector3.Distance(transform.position, data.Position);
            if (distToSound > data.Radius)
            {
                return false;
            }

            confidence = 1f - Mathf.Clamp01(distToSound / data.Radius);
            return true;
        }

        public LayerMask ObstacleLayer => obstacleLayer;
        public ElectricArcEffect ElectricEffectPrefab => electricEffectPrefab;
        public Transform GrabCameraTarget => grabCameraTarget;
        public EnemyMeleeHitbox MeleeHitbox => meleeHitbox;
        public BoneChainHitbox ArmSweepHitbox => armSweepHitbox;

        /// <summary>通常攻撃2で、地面に刺した鎌を引きずる時の火花。</summary>
        public TBossGroundDragSparks GroundDragSparks => groundDragSparks;

        /// <summary>ワイヤー側の腕をしなやかに動かすドライバ。Awakeで生成される。</summary>
        public TBossLimbChainDriver WireChainDriver { get; private set; }

        /// <summary>行動に合わせて鳴らす音。同じ GameObject の TutorialBossSounds をAwakeで取得する。</summary>
        public TutorialBossSounds Sounds { get; private set; }

        /// <summary>
        /// TransitionToAttackが選択しうる攻撃ステートの種別。
        /// </summary>
        private enum TBossAttackType
        {
            Normal,
            Normal2,
            Special,
        }

        /// <summary>視界判定・発見度・既知のプレイヤー位置を受け持つ。Awakeで生成される。</summary>
        public TBossPerception Perception { get; private set; }

        /// <summary>地形・障害物から攻撃や直線接近が成立するかを判定する。Awakeで生成される。</summary>
        public TBossAttackFeasibility AttackFeasibility { get; private set; }

        /// <summary>
        /// NavMeshAgent を止める／動かす。各ステートは Agent.isStopped を直接書かずにこれを使う。
        /// 見渡しモーションの最中は、動かす要求でも見渡しが終わるまで止めたままにする（滑って動くのを防ぐ）。
        /// </summary>
        public void SetAgentStopped(bool stopped)
        {
            _agentStopRequested = stopped;
            ApplyAgentStopped();
        }

        /// <summary>
        /// 周りを見渡すモーションを出す。再生しきるまでその場に止まる。
        /// 巡回・捜索・音の調査の間だけ有効で、追跡や攻撃に移ると自動で打ち切られる。
        /// </summary>
        public void PlayLookAround()
        {
            _lookAround.Play();
            ApplyAgentStopped();
        }

        private void ApplyAgentStopped()
        {
            // isStopped は NavMesh 上に無いエージェントで触るとエラーになる（死亡時は Agent 自体を無効化している）
            if (Agent == null || !Agent.isOnNavMesh)
            {
                return;
            }

            bool stop = _agentStopRequested || _lookAround.IsHolding;
            if (stop && !Agent.isStopped)
            {
                // 減速を待つと、その間に滑って見えるため即座に止める
                Agent.velocity = Vector3.zero;
            }
            Agent.isStopped = stop;
        }

        /// <summary>
        /// 前のステートが引いたトリガーが、遷移の途中などで使われずに残っていると、後で状況と無関係に再生されるため捨てる。
        /// 次のステートが必要なトリガーはその Enter で引き直す。
        /// </summary>
        protected override void OnBeforeStateEnter()
        {
            // ここではまだ currentState が切り替わる前のステート。様子見の入り方（攻撃の後か等）の判断に使う
            _previousState = State;

            if (Animator == null)
            {
                return;
            }

            foreach (string trigger in ActionTriggers)
            {
                Animator.ResetTrigger(trigger);
            }
        }

        /// <summary>
        /// 見渡しの足止めを1フレーム分進める。見渡してよいのは巡回・捜索・音の調査の間だけで、
        /// それ以外（追跡・攻撃など）に移ったら足止めを解く（モーションの打ち切りは Animator 側の遷移に任せる）。
        /// </summary>
        private void UpdateLookAround()
        {
            bool lookAllowed = State is { AllowsLookAround: true };
            if (_lookAround.Tick(lookAllowed, Time.deltaTime))
            {
                ApplyAgentStopped();
            }
        }

        /// <summary>交戦中で、次の攻撃を仕掛けてよい時刻を過ぎたか。</summary>
        public bool IsAttackDue => _combatTempo.IsAttackDue;

        /// <summary>次の攻撃を仕掛けてよい時刻までの残り秒数（デバッグ表示・ログ用）。交戦の時計が動いていなければ負。</summary>
        public float TimeUntilNextAttack =>
            _combatTempo.IsScheduled ? Mathf.Max(0f, _combatTempo.NextAttackTime - Time.time) : -1f;

        // 今のステート。開始前（Start より前）は null
        private TBossBaseState State => currentState as TBossBaseState;

        /// <summary>今のステートの名前（デバッグ表示・ログ用）。</summary>
        public string CurrentStateName =>
            currentState != null ? currentState.GetType().Name : "なし";

        /// <summary>追跡・様子見に入った時に呼ぶ。交戦の時計がまだ動いていなければ動かし始める。</summary>
        public void BeginCombatTempo() => _combatTempo.EnsureScheduled(CurrentAttackInterval);

        /// <summary>攻撃と攻撃の間隔の基準（秒）。焦りへの追い込み（短く）・息継ぎ（長く）で補正済み。</summary>
        private float CurrentAttackInterval =>
            _engagement.AttackInterval * PanicPressure.AttackIntervalScale;

        /// <summary>交戦が終わった時（巡回へ戻る等）に呼ぶ。次の交戦では待ち時間を新しく決める。</summary>
        public void EndCombatTempo() => _combatTempo.Clear();

        /// <summary>プレイヤーの焦りに応じた追い込みの判断。Awakeで生成される。</summary>
        public TBossPanicPressure PanicPressure { get; private set; }

        /// <summary>防御の判断と、プレイヤーの攻撃の受け止め。Awakeで同じ GameObject から取得する。</summary>
        public TBossGuard Guard { get; private set; }

        /// <summary>プレイヤーが倒れているか。倒れたプレイヤーは知覚・攻撃の対象にしない。</summary>
        public bool IsPlayerDead => _playerStatus != null && _playerStatus.IsDead;

        /// <summary>
        /// 顔（と頭に付けた懐中電灯）をプレイヤーへ向ける状態か。追跡・様子見の間と、巡回中に怪しんで見つめている間だけ true。
        /// 攻撃・怯み・捕獲・未発見時はアニメーションに任せ、攻撃後の硬直中に視界から抜ける逃げ道を残す。
        /// </summary>
        public bool IsHeadAimActive => State is { AimsHeadAtPlayer: true };

        /// <summary>
        /// 通常攻撃・通常攻撃2（鎌）・特殊攻撃（触手）・はじき返しのステート中か。走り込みの接近フェーズも含む。
        /// 捕獲は攻撃が当たった後の別ステートなので含めない。
        /// </summary>
        public override bool IsAttacking => State is { IsAttack: true };

        // ────────────────────────────────────────────
        //  Unity ライフサイクル
        // ────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (wireBone != null)
            {
                InitialWireBoneLocalPosition = wireBone.localPosition;
                InitialWireBoneLocalRotation = wireBone.localRotation;
            }

            Assert.IsNotNull(
                armSweepHitbox,
                $"{name}: 腕全体の攻撃判定（armSweepHitbox）が未設定です"
            );
            Assert.IsNotNull(
                groundDragSparks,
                $"{name}: 引きずりの火花（groundDragSparks）が未設定です"
            );

            WireChainDriver = new TBossLimbChainDriver(wireChainSettings);

            Perception = new TBossPerception(this);
            _lookAround = new TBossLookAround(Animator);
            AttackFeasibility = new TBossAttackFeasibility(this, Perception);

            if (Player != null)
            {
                _playerStatus = Player.GetComponent<PlayerStatus>();
                Assert.IsNotNull(_playerStatus, $"{name}: プレイヤーに PlayerStatus がありません");

                _playerController = Player.GetComponent<PlayerController>();
                Assert.IsNotNull(
                    _playerController,
                    $"{name}: プレイヤーに PlayerController がありません（弓を構えたのを見て防御できません）"
                );

                _playerInput = Player.GetComponent<PlayerInputHandler>();
                Assert.IsNotNull(
                    _playerInput,
                    $"{name}: プレイヤーに PlayerInputHandler がありません（攻撃入力に反応して防御できません）"
                );

                _playerPanic = Player.GetComponent<PanicDetector>();
                if (_playerPanic == null)
                {
                    Debug.LogWarning(
                        $"{name}: プレイヤーに PanicDetector が無いため、焦りへの追い込みは行いません"
                    );
                }
            }

            PanicPressure = new TBossPanicPressure(panicPressureSettings);
            PanicPressure.PressureStarted += OnPressureStarted;
            PanicPressure.PressureEnded += OnPressureEnded;

            if (!TryGetComponent(out _eyeGlint))
            {
                Debug.LogWarning(
                    $"{name}: TBossEyeGlint が無いため、追い込み開始の合図は音だけになります"
                );
            }

            Sounds = TryGetComponent(out TutorialBossSounds sounds) ? sounds : null;
            Assert.IsNotNull(Sounds, $"{name}: TutorialBossSounds が同じ GameObject にありません");

            Guard = TryGetComponent(out TBossGuard guard) ? guard : null;
            Assert.IsNotNull(Guard, $"{name}: TBossGuard が同じ GameObject にありません");
        }

        protected override void Start()
        {
            base.Start();
            SetScytheTrailVisible(false);
            ChangeState(new TBossPatrolState(this));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_playerStatus != null)
            {
                _playerStatus.Damaged += OnPlayerDamaged;
            }
            if (_playerInput != null)
            {
                _playerInput.AttackPressed += OnPlayerAttackPressed;
            }
            Guard.Repelled += OnRepelled;
            BowController.Fired += OnPlayerRangedAttack;
            ScytheController.Thrown += OnPlayerRangedAttack;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_playerStatus != null)
            {
                _playerStatus.Damaged -= OnPlayerDamaged;
            }
            if (_playerInput != null)
            {
                _playerInput.AttackPressed -= OnPlayerAttackPressed;
            }
            Guard.Repelled -= OnRepelled;
            BowController.Fired -= OnPlayerRangedAttack;
            ScytheController.Thrown -= OnPlayerRangedAttack;
        }

        /// <summary>
        /// プレイヤーが攻撃ボタンを押した瞬間に呼ばれる。様子見中なら、一定の確率で反応して防御する（反応の防御）。
        /// </summary>
        private void OnPlayerAttackPressed()
        {
            if (State is not { ReactsToAttackInput: true } || !Guard.RollReactiveGuard())
            {
                return;
            }

            ChangeState(
                new TBossGuardState(
                    this,
                    Guard.Settings.ReactiveGuardDuration,
                    attackWhenDone: false
                )
            );
        }

        /// <summary>
        /// 様子見に入った直後に呼ぶ。攻撃の後なら「攻撃後の防御」を、HPの段階に応じた確率で引く。
        /// 追跡などから入ったなら「読みの防御」を引き、当たれば短く防御してから攻撃する。
        /// 防御・はじき返しの後に戻ってきた時は引かない（防御が続けて出るのを防ぐ）。
        /// 防御に切り替えたら true。
        /// </summary>
        public bool TryStartOpeningGuard()
        {
            if (_previousState is { BlocksOpeningGuardAfter: true })
            {
                return false;
            }

            if (_previousState is { TriggersPostAttackGuard: true })
            {
                if (!Guard.RollPostAttackGuard())
                {
                    return false;
                }
                ChangeState(
                    new TBossGuardState(
                        this,
                        Guard.Settings.PostAttackGuardDuration,
                        attackWhenDone: false
                    )
                );
                return true;
            }

            if (!Guard.RollReadGuard(out float duration))
            {
                return false;
            }
            ChangeState(new TBossGuardState(this, duration, attackWhenDone: true));
            return true;
        }

        /// <summary>
        /// 様子見・追跡・防御の間だけ、連打されたらはじき返せるようにする。攻撃・怯みなど、それ以外の行動中に殴られた分は数えない。
        /// </summary>
        private void UpdateRepelTrigger()
        {
            Guard.CanRepel = State is { CanRepel: true } && Player != null && !IsPlayerDead;
            if (!Guard.CanRepel)
            {
                Guard.ClearMash();
            }
        }

        /// <summary>弓を構え始めた瞬間を見て、遠距離の防御を引く（撃った・投げた瞬間はイベントで受ける）。</summary>
        private void UpdateRangedGuardTrigger()
        {
            if (_playerController == null)
            {
                return;
            }

            bool aiming = _playerController.IsAiming;
            if (aiming && !_wasPlayerAiming)
            {
                TryStartRangedGuard();
            }
            _wasPlayerAiming = aiming;
        }

        private void OnPlayerRangedAttack()
        {
            State?.OnPlayerRangedAttack();
            TryStartRangedGuard();
        }

        /// <summary>
        /// 明らかに距離を取って遠距離攻撃をしてきそうな時（様子見の間合いの外で構える・撃つ・投げる）に確率を引き、
        /// 当たれば構えてにじり寄る。外れたら普段どおり追いかける。続けて引きすぎないよう間隔を空ける。
        /// </summary>
        private void TryStartRangedGuard()
        {
            if (
                State is not { IsEngaging: true }
                || Player == null
                || IsPlayerDead
                || Time.time < _nextRangedGuardRollTime
            )
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, Player.transform.position);
            if (distance <= _engagement.WatchDistanceMax)
            {
                return;
            }

            _nextRangedGuardRollTime = Time.time + Guard.Settings.RangedGuardRollInterval;
            if (Guard.RollRangedGuard())
            {
                ChangeState(new TBossGuardAdvanceState(this));
            }
        }

        /// <summary>連打の最後の一撃が当たったその瞬間に、払いを始める（プレイヤーののけぞりも同じフレームで始まる）。</summary>
        private void OnRepelled(PlayerAttackKind kind, Vector3 hitPoint)
        {
            ChangeState(new TBossRepelState(this, kind));
        }

        /// <summary>
        /// プレイヤーが被弾した時に呼ばれる。このボスの攻撃・掴みによる被弾だけを、追い込みの歯止め（命中数）に数える。
        /// 掴み中の電撃は一定間隔の継続ダメージなので、掴み1回を1命中として数える。
        /// </summary>
        private void OnPlayerDamaged()
        {
            if (State is { IsCapturingPlayer: true })
            {
                if (_hasCountedCurrentCapture)
                {
                    return;
                }
                _hasCountedCurrentCapture = true;
            }
            else if (!IsAttacking)
            {
                return; // このボスの攻撃中ではない被弾（他の敵・地形など）は数えない
            }

            PanicPressure.NotifyBossHitPlayer();
        }

        protected override void Update()
        {
            // ステートの更新より先に見る。ステート内のダメージ処理中に死亡イベントで遷移すると、
            // Exit後のステートが処理を続けてしまうため、イベント購読ではなくここで状態を確認する
            DisengageIfPlayerDied();
            base.Update();
            UpdateLookAround();
            TryRestorePlayerCollision();
            UpdateAnimatorParameters();
            Perception.Tick(Time.deltaTime);
            TickPanicPressure();
            UpdateRepelTrigger();
            UpdateRangedGuardTrigger();

            if (State is not { IsCapturingPlayer: true })
            {
                _hasCountedCurrentCapture = false;
            }
        }

        /// <summary>
        /// プレイヤーの焦りの段階・系統から、追い込みの判断を1フレーム分進める。
        /// 交戦していない（未発見・プレイヤー死亡・ボス死亡）間は追い込まない。
        /// </summary>
        private void TickPanicPressure()
        {
            if (_playerPanic == null)
            {
                return; // Awake で警告済み。追い込みなしで動く
            }

            bool isEngaged =
                IsAlerted
                && !IsPlayerDead
                && _playerPanic.IsInCombat
                && State is not { IsDead: true };
            PanicPressure.Tick(
                _playerPanic.Level,
                _playerPanic.DominantCategory,
                isEngaged,
                Time.deltaTime
            );
        }

        /// <summary>
        /// 追い込みを始めた瞬間の合図。目をギラッと光らせ、同時に音を鳴らす。追い込み中は目に残光を残す。
        /// 背を向けて逃げているプレイヤーには目が見えないため、音も必ず鳴らす。
        /// </summary>
        private void OnPressureStarted(PanicCategory category)
        {
            DevLog.Log($"[TutorialBoss] 追い込み開始（{category.ToDisplayName()}）");

            Vector3 cuePosition = transform.position;
            if (_eyeGlint != null)
            {
                _eyeGlint.Play();
                _eyeGlint.SetSustain(true);
                cuePosition = _eyeGlint.CuePosition;
            }
            Sounds.PlayPressureCue(cuePosition);

            // 次の攻撃が遠ければ追い込みの間隔で前倒しする。合図から攻撃までは最低限の間を取る
            _combatTempo.Expedite(CurrentAttackInterval, PanicPressure.CueLeadSeconds);
        }

        /// <summary>追い込みが終わった（息継ぎに入った・相手が落ち着いた）ら、目の残光を消す。</summary>
        private void OnPressureEnded()
        {
            DevLog.Log($"[TutorialBoss] 追い込み終了（{PanicPressure.Mode}）");

            if (_eyeGlint != null)
            {
                _eyeGlint.SetSustain(false);
            }
        }

        /// <summary>
        /// プレイヤーが倒れたら交戦をやめて巡回へ戻る。倒れたプレイヤーは操作を止められて動けないため、
        /// これが無いと延々と攻撃し続ける。
        /// </summary>
        private void DisengageIfPlayerDied()
        {
            if (!IsPlayerDead)
            {
                _hasDisengagedFromDeadPlayer = false;
                return;
            }

            if (_hasDisengagedFromDeadPlayer || State is { IsDead: true })
            {
                return;
            }

            _hasDisengagedFromDeadPlayer = true;
            DevLog.Log("[TutorialBoss] プレイヤーが倒れたため交戦を終了");
            SetAlerted(false);
            Perception.ForgetPlayer();
            ChangeState(new TBossPatrolState(this));
        }

        /// <summary>鎌の軌跡を出す／止める。</summary>
        public void SetScytheTrailVisible(bool visible)
        {
            scytheTrail.emitting = visible;
        }

        /// <summary>
        /// ボスとプレイヤーの体（コライダー）が水平方向で触れ合う中心間の距離。
        /// ボスのカプセル半径とプレイヤーのキャラクターコントローラー半径を、それぞれの拡大率込みで足す。
        /// </summary>
        public float BodyContactDistance
        {
            get
            {
                float bossRadius = EnemyCollider is CapsuleCollider capsule
                    ? capsule.radius * transform.lossyScale.x
                    : EnemyCollider.bounds.extents.x;
                float playerRadius = PlayerCollider is CharacterController playerCC
                    ? playerCC.radius * Player.transform.lossyScale.x
                    : PlayerCollider.bounds.extents.x;
                return bossRadius + playerRadius;
            }
        }

        /// <summary>攻撃の動きでボスがプレイヤーを押し退けたり、引っかかったりしないよう、互いの衝突を無効にする。</summary>
        public void IgnorePlayerCollision()
        {
            if (EnemyCollider == null || PlayerCollider == null)
            {
                return;
            }

            _isPlayerCollisionRestorePending = false;
            Physics.IgnoreCollision(EnemyCollider, PlayerCollider, true);
        }

        /// <summary>
        /// 無効にした衝突を戻す。体が重なっている間は戻さず、離れた時点で戻す
        /// （重なったまま戻すと、プレイヤーのキャラクターコントローラーがボスのカプセルを段差として乗り越え、上へ飛ばされる）。
        /// </summary>
        public void RestorePlayerCollisionWhenSeparated()
        {
            _isPlayerCollisionRestorePending = true;
            TryRestorePlayerCollision();
        }

        private void TryRestorePlayerCollision()
        {
            if (
                !_isPlayerCollisionRestorePending
                || EnemyCollider == null
                || PlayerCollider == null
            )
            {
                return;
            }

            Vector3 toPlayer = Player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude < BodyContactDistance + SeparationMargin)
            {
                return;
            }

            Physics.IgnoreCollision(EnemyCollider, PlayerCollider, false);
            _isPlayerCollisionRestorePending = false;
        }

        /// <summary>
        /// アニメーターのブレンドツリーがキャラクター基準の相対的な移動方向を要求するため、
        /// NavMeshAgentのワールド速度をローカル座標系に変換して適用する。
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (Animator == null || Agent == null)
            {
                return;
            }
            Vector3 localVelocity = transform.InverseTransformDirection(Agent.velocity);
            Animator.SetFloat(VelocityXHash, localVelocity.x);
            Animator.SetFloat(VelocityZHash, localVelocity.z);
            UpdateRunAnimation();
        }

        /// <summary>
        /// Animatorの IsRunning を実際の移動速度から決める。
        /// 各ステートが「走る」と宣言したまま経路に詰まると、その場で走りモーションを続けて固まって見えるため、
        /// 見た目は常に実速度に従わせる。
        /// </summary>
        private void UpdateRunAnimation()
        {
            Vector3 velocity = Agent.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            float threshold = Mathf.Lerp(
                _movement.WalkSpeed,
                _movement.RunSpeed,
                _isRunAnimationActive ? RunAnimationExitSpeedRatio : RunAnimationEnterSpeedRatio
            );
            // isStopped は NavMesh 上に無いエージェントで参照するとエラーになるため isOnNavMesh を先に見る
            bool shouldRun = Agent.isOnNavMesh && !Agent.isStopped && speed >= threshold;

            if (shouldRun != _isRunAnimationActive)
            {
                _isRunAnimationActive = shouldRun;
                Animator.SetBool(IsRunningHash, shouldRun);
            }
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();

            // はじき返しは短い反撃なので、途中で殴られても怯まずにやり切る
            if (State is { IgnoresFlinch: true })
            {
                return;
            }
            ChangeState(new TBossFlinchState(this));
        }

        /// <summary>
        /// 被弾時（EnemyStatusが被弾ごとに無条件で呼ぶ）。
        /// 以前は警戒中なら何もしなかったため、捜索中や見失っている最中に背後から撃たれても反応しなかった。
        /// 今は「捕捉できていない時に撃たれたら、撃たれた方へ向かう」を警戒の有無に関係なく行う。
        /// </summary>
        public override void ForceAlert()
        {
            base.ForceAlert();

            // 攻撃・捕獲・怯み・死亡の最中は、その行動をやり切らせる（被弾のたびに差し替えると攻撃が成立しない）
            if (State is { IsCommitted: true } || Player == null || IsPlayerDead)
            {
                return;
            }

            SetAlerted(true);

            if (Perception.IsPlayerTracked())
            {
                // 見えているなら交戦する。既に追跡・様子見中ならそのまま続ける
                if (State is not { IsEngaging: true })
                {
                    ChangeState(new TBossChaseState(this));
                }
                return;
            }

            // 光の外・背後・物陰から撃たれた。EnemyStatusは攻撃者の座標を渡してこないため、
            // 現在のプレイヤー位置を推定攻撃元として既知位置に入れ、追跡（見失い中の挙動＝そこへ走る）で向かわせる
            Perception.NotifyDamageFrom(Player.transform.position);
            ChangeState(new TBossChaseState(this));
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            ChangeState(new TBossDeathState(this));
        }

        /// <summary>近接攻撃（通常攻撃・鎌）をジャストパリィされた時に剣側から呼ばれ、大きく崩れる。</summary>
        public override void ForceParried()
        {
            base.ForceParried();
            if (State is { IsDead: true })
            {
                return;
            }
            ChangeState(new TBossParriedState(this));
        }

        // ────────────────────────────────────────────
        //  アニメーションイベント（クリップから関数名で呼ばれる。今のステートへ渡すだけ）
        // ────────────────────────────────────────────

        private void HandleAnimationEvent(TBossAnimationEvent animationEvent) =>
            State?.OnAnimationEvent(animationEvent);

        /// <summary>通常攻撃の当たり判定を入れるAnimationEvent。</summary>
        public void TriggerEnableHitbox() =>
            HandleAnimationEvent(TBossAnimationEvent.MeleeHitStart);

        /// <summary>通常攻撃の当たり判定を切るAnimationEvent。</summary>
        public void TriggerDisableHitbox() => HandleAnimationEvent(TBossAnimationEvent.MeleeHitEnd);

        /// <summary>鎌の軌跡を出し始めるAnimationEvent。</summary>
        public void ShowScytheTrail() => HandleAnimationEvent(TBossAnimationEvent.ScytheTrailShow);

        /// <summary>鎌の軌跡を止めるAnimationEvent。</summary>
        public void HideScytheTrail() => HandleAnimationEvent(TBossAnimationEvent.ScytheTrailHide);

        /// <summary>特殊攻撃のアニメーション中、一番ワイヤーを射出するのに適したポーズで呼ばれるAnimationEvent。</summary>
        public void OnSpecialAttackPoseReady() =>
            HandleAnimationEvent(TBossAnimationEvent.SpecialAttackPoseReady);

        /// <summary>通常攻撃2で、腕を伸ばして振りに入る箇所で呼ばれるAnimationEvent。1クリップ内で複数回呼んでよい。</summary>
        public void OnAttack2ReachExtend() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2ReachExtend);

        /// <summary>通常攻撃2で、腕を戻し終えた箇所で呼ばれるAnimationEvent。1クリップ内で複数回呼んでよい。</summary>
        public void OnAttack2ReachRetract() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2ReachRetract);

        /// <summary>通常攻撃2で、伸びた腕全体の攻撃判定を始めるAnimationEvent。振り始めに置く。</summary>
        public void EnableArmSweepHitbox() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2ArmSweepStart);

        /// <summary>通常攻撃2で、伸びた腕全体の攻撃判定を止めるAnimationEvent。振り切った直後に置く。</summary>
        public void DisableArmSweepHitbox() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2ArmSweepEnd);

        /// <summary>通常攻撃2で、振り下ろした鎌が地面に刺さった瞬間に呼ばれるAnimationEvent。</summary>
        public void OnAttack2GroundImpact() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2GroundImpact);

        /// <summary>通常攻撃2で、地面に刺した鎌を引きずり始める箇所で呼ばれるAnimationEvent。</summary>
        public void OnAttack2GroundDragStart() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2GroundDragStart);

        /// <summary>通常攻撃2で、鎌が地面から離れる箇所で呼ばれるAnimationEvent。</summary>
        public void OnAttack2GroundDragEnd() =>
            HandleAnimationEvent(TBossAnimationEvent.Attack2GroundDragEnd);

        /// <summary>
        /// 攻撃を仕掛けてみて、仕掛けられなければ追跡へ戻して間合いを詰め直す。様子見から使う。
        /// </summary>
        public void TransitionToAttack()
        {
            if (!TryStartAttack())
            {
                ChangeState(new TBossChaseState(this));
            }
        }

        /// <summary>
        /// プレイヤーとの距離・地形・各攻撃のクールダウン・直前の選択履歴から攻撃を選び、仕掛けられれば遷移して true を返す。
        /// どの攻撃も成立しなければ何もせず false を返す（追跡中から呼んでも追跡を差し替えない）。
        /// </summary>
        public bool TryStartAttack() =>
            TryStartAttack(allowNormalAttack: true, ignoreCooldowns: false);

        /// <summary>
        /// はじき返した直後の追い打ち。通常攻撃2か掴みだけから選ぶ（通常攻撃は速すぎて、下がっている相手には避けようがないため除く）。
        /// はじき返しへの返しなので、クールダウンは見ずに、間合い・地形だけで選ぶ。仕掛けられなければ false。
        /// </summary>
        public bool TryStartRepelFollowUp() =>
            TryStartAttack(allowNormalAttack: false, ignoreCooldowns: true);

        private bool TryStartAttack(bool allowNormalAttack, bool ignoreCooldowns)
        {
            if (Player == null || IsPlayerDead)
            {
                return false;
            }

            float distance = Vector3.Distance(transform.position, Player.transform.position);

            // 地形の判定。段差の上下には近接が届かず、壁越しには振りも触手も通らない。
            // 近接は走り込む接近フェーズで回り込めるため、射程外なら高さだけを見て、射線は振る直前に確かめる。
            bool meleeHeightOk = AttackFeasibility.IsWithinMeleeHeight();

            bool normalEligible =
                allowNormalAttack
                && meleeHeightOk
                && distance <= Mathf.Max(_normalAttack.Range, _engagement.MeleeChargeRange)
                && (ignoreCooldowns || Time.time >= _normalAttackReadyTime);
            bool normal2Eligible =
                meleeHeightOk
                && distance <= Mathf.Max(_normalAttack2.Range, _engagement.MeleeChargeRange)
                && (ignoreCooldowns || Time.time >= _normalAttack2ReadyTime)
                && (distance > _normalAttack2.Range || AttackFeasibility.HasMeleeLine());
            // 特殊攻撃（触手の拘束）は至近距離で使うと不自然なため、通常攻撃の射程より外側でのみ選択可能にする。
            // 空振りすると長いクールダウンを無駄にするため、射線が通っている時だけ選ぶ（重い判定なので最後に評価）
            bool specialEligible =
                distance > _normalAttack.Range
                && distance <= _specialAttack.Range
                && (ignoreCooldowns || Time.time >= _specialAttackReadyTime)
                && AttackFeasibility.HasClearWirePath();

            SuppressRepeatedChoice(ref normalEligible, ref normal2Eligible, ref specialEligible);

            if (!normalEligible && !normal2Eligible && !specialEligible)
            {
                // 間合いや射線が合っていない、または全てクールダウン中
                return false;
            }

            // 攻撃に入る時点で警戒状態を確定させる。
            // EnemyStatusは被弾ごとにOnAlertTriggeredを無条件発火するため、IsAlertedがfalseのままだと
            // ForceAlertが走って攻撃モーションや捕獲シーケンスが強制的にChaseへ差し替えられてしまう。
            SetAlerted(true);
            CommitAttack(ChooseWeightedAttack(normalEligible, normal2Eligible, specialEligible));
            return true;
        }

        /// <summary>
        /// 同じ攻撃が3回連続で選ばれるのを防ぐ。ただし抑制すると候補が0になる場合は
        /// AIが手詰まりで棒立ちになるのを避けるため抑制しない。
        /// </summary>
        private void SuppressRepeatedChoice(
            ref bool normalEligible,
            ref bool normal2Eligible,
            ref bool specialEligible
        )
        {
            if (_lastAttackRepeatCount < MaxAttackRepeatBeforeAvoid || _lastAttackType == null)
            {
                return;
            }

            switch (_lastAttackType.Value)
            {
                case TBossAttackType.Normal when normal2Eligible || specialEligible:
                    normalEligible = false;
                    break;
                case TBossAttackType.Normal2 when normalEligible || specialEligible:
                    normal2Eligible = false;
                    break;
                case TBossAttackType.Special when normalEligible || normal2Eligible:
                    specialEligible = false;
                    break;
            }
        }

        /// <summary>
        /// 選択可能な候補の中で、Inspectorの確率設定を重みとして正規化し抽選する。
        /// 通常攻撃は確率スライダーを持たないため基準枠（重み1）として扱う。
        /// 焦っているプレイヤーには、焦りの系統に合わせて重みに倍率を掛ける（例: 逃げ腰には触手を選びやすく）。
        /// 間合い・射線・クールダウンで候補を絞った後に掛けるので、届かない攻撃を無理に選ぶことは無い。
        /// </summary>
        private TBossAttackType ChooseWeightedAttack(
            bool normalEligible,
            bool normal2Eligible,
            bool specialEligible
        )
        {
            float specialWeight = specialEligible
                ? _specialAttack.Chance * PanicPressure.SpecialWeightScale
                : 0f;
            float normal2Weight = normal2Eligible
                ? _normalAttack2.Chance * PanicPressure.Normal2WeightScale
                : 0f;
            float normalWeight = normalEligible ? PanicPressure.NormalWeightScale : 0f;

            float total = specialWeight + normal2Weight + normalWeight;
            if (total <= 0f)
            {
                // 確率スライダーが0でも、間合いが合っている攻撃自体は選択できなければならない
                if (specialEligible)
                {
                    return TBossAttackType.Special;
                }
                if (normal2Eligible)
                {
                    return TBossAttackType.Normal2;
                }
                return TBossAttackType.Normal;
            }

            float roll = Random.value * total;

            if (roll < specialWeight)
            {
                return TBossAttackType.Special;
            }
            roll -= specialWeight;
            if (roll < normal2Weight)
            {
                return TBossAttackType.Normal2;
            }
            return TBossAttackType.Normal;
        }

        /// <summary>
        /// 選択した攻撃のクールダウンを開始し、連続選択カウントを更新してステート遷移する。
        /// </summary>
        private void CommitAttack(TBossAttackType type)
        {
            // 次の攻撃までの待ち時間はここで決め直す（様子見・追跡のどちらから仕掛けても同じ時計で数える）
            _combatTempo.Schedule(CurrentAttackInterval);

            if (_lastAttackType == type)
            {
                _lastAttackRepeatCount++;
            }
            else
            {
                _lastAttackType = type;
                _lastAttackRepeatCount = 1;
            }

            switch (type)
            {
                case TBossAttackType.Special:
                    _specialAttackReadyTime = Time.time + _specialAttack.Cooldown;
                    ChangeState(new TBossSpecialAttackState(this));
                    break;
                case TBossAttackType.Normal2:
                    _normalAttack2ReadyTime = Time.time + _normalAttack2.Cooldown;
                    ChangeState(new TBossNormalAttack2State(this));
                    break;
                default:
                    _normalAttackReadyTime = Time.time + _normalAttack.Cooldown;
                    ChangeState(new TBossNormalAttackState(this));
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 攻撃射程
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _normalAttack.Range);

            // 通常攻撃2射程
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _normalAttack2.Range);

            // 特殊攻撃射程
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, _specialAttack.Range);

            // 交戦中に光の外でも捕捉し続ける距離
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, _sight.CombatCloseSenseRadius);
        }
#endif
    }
}
