using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボス専用のAIコントローラ。
    /// 子オブジェクトの懐中電灯（Spotlight）による視界判定と、
    /// SoundEventBusを通じた音への反応を主軸とした行動制御を行う。
    /// </summary>
    public class TutorialBossController : EnemyBaseController
    {
        [Header("懐中電灯（視界）設定")]
        [Tooltip("懐中電灯オブジェクトのTransform。回転制御に使用する。")]
        [SerializeField]
        private Transform flashlightTransform;

        [Tooltip("懐中電灯のLightコンポーネント。SpotAngleとRangeを視界範囲として使用する。")]
        [SerializeField]
        private Light flashlightLight;

        [Tooltip("プレイヤーが光から外れてから見失うまでの時間（秒）。")]
        [SerializeField]
        private float lostSightDuration = 3f;

        [Tooltip(
            "交戦中（追跡・様子見）は、この水平距離（m）以内で遮蔽物が無ければ光の外でもプレイヤーを捕捉し続ける。\n"
                + "光円錐は細いため、0にすると至近距離で横に回り込まれただけで見失う。"
        )]
        [SerializeField]
        private float combatCloseSenseRadius = 6f;

        [Header("エフェクト")]
        [Tooltip("鎌のTrailEffect")]
        [SerializeField]
        private TrailRenderer scytheTrail;

        [Header("移動設定")]
        [Tooltip("未発見時の歩行速度。")]
        [SerializeField]
        private float walkSpeed = 1.5f;

        [Tooltip("発見時・音反応時の走り速度。")]
        [SerializeField]
        private float runSpeed = 5f;

        [Tooltip("発見時様子見移動の速度")]
        [SerializeField]
        private float strafeSpeed = 2f;

        [Header("距離設定")]
        [Tooltip("通常攻撃の攻撃射程。")]
        [SerializeField]
        private float attackRange = 2f;

        [Tooltip("通常攻撃2（腕伸ばし）の判定射程。")]
        [SerializeField]
        private float normalAttack2Range = 4f;

        [Tooltip("特殊攻撃（触手）の最大射程。")]
        [SerializeField]
        private float specialAttackRange = 10f;

        [Tooltip(
            "様子見で保つ間合いの内側（m、中心間の距離）。これより近いと下がる。体格が大きいので体同士が触れない程度に取る。"
        )]
        [SerializeField]
        private float watchDistanceMin = 6f;

        [Tooltip("様子見で保つ間合いの外側（m、中心間の距離）。これより遠いと詰める。")]
        [SerializeField]
        private float watchDistanceMax = 9f;

        [Tooltip(
            "通常攻撃・鎌は、この距離以内なら走り込んでから振る。様子見の間合いから直接仕掛けられるよう、様子見の外側より長くする。"
        )]
        [SerializeField]
        private float meleeChargeRange = 10f;

        [Header("タイマー設定")]
        [Tooltip(
            "攻撃と攻撃の間隔の基準（秒）。実際は0.6〜1.4倍でばらつく。様子見・追跡をまたいで数えるので、離れて仕切り直されても延びない。"
        )]
        [SerializeField]
        private float watchDuration = 2f;

        [Tooltip(
            "追跡で間合いに入った時、様子見を挟まずそのまま攻撃に移る確率（0〜1）。毎回様子見から入る一定パターンを崩す。"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float directAssaultChance = 0.35f;

        [Header("攻撃確率設定")]
        [Tooltip("攻撃ステート選択時に特殊攻撃を選ぶ確率（0〜1）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float specialAttackChance = 0.3f;

        [Tooltip("攻撃ステート選択時に通常攻撃2を選ぶ確率（0〜1）。特殊攻撃が外れた後に判定する。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float normalAttack2Chance = 0.4f;

        [Header("攻撃クールダウン設定")]
        [Tooltip("通常攻撃を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float normalAttackCooldown = 1f;

        [Tooltip("通常攻撃2を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float normalAttack2Cooldown = 2f;

        [Tooltip("特殊攻撃を選択できるようになるまでの再使用間隔（秒）。")]
        [SerializeField]
        private float specialAttackCooldown = 8f;

        [Header("特殊攻撃（ワイヤー）設定")]
        [Tooltip(
            "ワイヤー先端の当たり判定の半径（m）。先端の軌跡に沿ってこの太さで掃引し、プレイヤーに触れたら捕獲する。\n"
                + "攻撃前の射線チェックも同じ太さで行う。"
        )]
        [SerializeField]
        private float wireHitRadius = 1f;

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

        [Tooltip("AnimationRiggingのRigコンポーネント（捕獲時のプレイヤー追従ON/OFF用）。")]
        [SerializeField]
        private Rig wireRig;

        [Tooltip(
            "ワイヤーIKのターゲットTransform（捕獲時にプレイヤーの首の位置をコピーして追従させる）。"
        )]
        [SerializeField]
        private Transform wireIkTarget;

        [Tooltip("特殊攻撃の狙いフェーズの時間（秒）。")]
        [SerializeField]
        private float specialAttackAimDuration = 1.5f;

        [Tooltip("特殊攻撃の射出フェーズの時間（秒）。")]
        [SerializeField]
        private float specialAttackShootDuration = 0.3f;

        [Header("特殊攻撃 向き強制設定")]
        [Tooltip(
            "true: Rig(Position Constraint)を使ってワイヤーを正面に引っ張る。\nfalse: スクリプトで強制的に位置と回転を正面に上書きする。"
        )]
        [SerializeField]
        private bool forceStraightWireByRig = true;

        [Header("通常攻撃（踏み込み）設定")]
        [Tooltip(
            "踏み込み後に、プレイヤーの中心がボスの中心からこの距離に来るよう踏み込みの長さを合わせる（m）。\n"
                + "クリップの振りで当たり判定（半径1m）の中心が最も前に出るのは約3.0m先のため、その少し外を狙う。"
        )]
        [SerializeField]
        private float attack1StrikeDistance = 3.5f;

        [Tooltip(
            "踏み込みで動かす長さの上限（m）。踏み込み中も後ろ足は接地したままなので、長くすると後ろ足が引きずられて見える。"
        )]
        [SerializeField]
        private float attack1MaxStepIn = 1.5f;

        [Header("通常攻撃2（鎌）設定")]
        [Tooltip("鎌の付け根として伸縮・角度補正を行うボーン。左手（mixamorig:LeftHand）を想定。")]
        [SerializeField]
        private Transform kamaBone;

        [Tooltip(
            "鎌ボーンの初期ローカル座標（リトラクト時に戻す位置）。実行時に自動取得されます。"
        )]
        [HideInInspector]
        public Vector3 InitialKamaBoneLocalPosition;

        [Tooltip(
            "鎌ボーンの初期ローカル回転（リトラクト時に戻す姿勢）。実行時に自動取得されます。"
        )]
        [HideInInspector]
        public Quaternion InitialKamaBoneLocalRotation = Quaternion.identity;

        [Tooltip("鎌が伸びる方向を示すkamaBoneのローカル軸。通常はUp(0,1,0)のままで良い。")]
        [SerializeField]
        private Vector3 kamaExtendLocalAxis = Vector3.up;

        [Tooltip(
            "リーチ上昇時に伸ばす長さ（メートル）。\n"
                + "このモデルの腕は肩から先端まで約0.74mしかないため、0.2〜0.3程度から調整する。"
                + "1mなどを指定すると腕が倍以上に伸びて軌道が破綻する。"
        )]
        [SerializeField]
        private float attack2ReachExtension = 0.25f;

        [Tooltip("リーチを伸ばす間、アニメーション速度を落とす倍率（0=完全停止、1=通常速度）。")]
        [SerializeField]
        [Range(0f, 1f)]
        private float attack2ReachHoldAnimatorSpeed = 0f;

        [Tooltip(
            "リーチを伸ばすために低速/停止させておく時間（秒、実時間）。この間に鎌を伸ばしきる。"
        )]
        [SerializeField]
        private float attack2ReachHoldDuration = 0.3f;

        [Tooltip("鎌の伸縮（伸ばす/戻す共通）にかける時間（秒）。")]
        [SerializeField]
        private float attack2ReachLerpDuration = 0.2f;

        [Tooltip(
            "通常攻撃2で、振りに入る前にプレイヤーへ向き直る速度。0で向き直らない。\n"
                + "リーチを伸ばし始めた時点（OnAttack2ReachExtend）で向きを固定するため、\n"
                + "振りに入った後の回避は可能なまま、振りの前だけ狙いを付け直す。"
        )]
        [SerializeField]
        private float attack2HomingRotateSpeed = 6f;

        [Tooltip(
            "ヒット時にプレイヤーを引き寄せる距離（メートル）。理不尽な強制引き寄せにならないよう小さめに。"
        )]
        [SerializeField]
        private float attack2PullDistance = 0.6f;

        [Tooltip("ヒット時の引き寄せにかける時間（秒）。")]
        [SerializeField]
        private float attack2PullDuration = 0.15f;

        [Tooltip(
            "Attack/Attack2共通で使う近接ヒットボックス。ヒット確定演出（引き寄せ）の判定にも使う。"
        )]
        [SerializeField]
        private EnemyMeleeHitbox meleeHitbox;

        [Header("しなやかな腕（ボーン連鎖）設定")]
        [Tooltip("特殊攻撃（ワイヤー）側の腕の連鎖。根元→先端の順に設定する。")]
        [SerializeField]
        private TBossLimbChainSettings wireChainSettings = new TBossLimbChainSettings();

        [Tooltip("通常攻撃2（鎌）側の腕の連鎖。根元→先端の順に設定する。")]
        [SerializeField]
        private TBossLimbChainSettings kamaChainSettings = new TBossLimbChainSettings();

        [Header("障害物設定")]
        [SerializeField]
        private LayerMask obstacleLayer;

        [Header("掴み（捕獲）設定")]
        [Tooltip("電撃ダメージ（1ティックあたり）の攻撃力倍率。実ダメージ = 敵攻撃力 × 倍率。")]
        [SerializeField]
        private float grabDamageMultiplier = 0.2f;

        [Tooltip("電撃ダメージを与える間隔（秒）。")]
        [SerializeField]
        private float grabDamageInterval = 0.5f;

        [Tooltip("脱出に必要なゲージの最大値。")]
        [SerializeField]
        private float grabEscapeThreshold = 20f;

        [Tooltip("移動ボタン1入力あたりのゲージ増加量。")]
        [SerializeField]
        private float grabEscapePerInput = 1f;

        [Tooltip("プレイヤーを引き寄せる際にかかる時間（秒）。")]
        [SerializeField]
        private float grabPullDuration = 0.5f;

        [Tooltip("プレイヤーを引き寄せる目標距離（ボス正面方向）。")]
        [SerializeField]
        private float grabPullDistance = 1.5f;

        [Tooltip(
            "プレイヤーを引き寄せる目標の横方向オフセット（ボスのright方向）。正値で右、負値で左にずれる。カメラ演出に応じて調整する。"
        )]
        [SerializeField]
        private float grabPullLateralOffset = 0f;

        [Tooltip("電撃エフェクトの ParticleSystem プレハブ（プレイヤーにアタッチして使用）。")]
        [SerializeField]
        private ParticleSystem electricEffectPrefab;

        [Tooltip("触手の引っ込み演出にかかる時間（秒）。捕獲脱出後のリトラクト。")]
        [SerializeField]
        private float grabRetractDuration = 0.4f;

        [Tooltip("電撃ダメージフェーズ用カメラ(DamageSceneCamera)の追従ターゲット(GrabTarget)。")]
        [SerializeField]
        private Transform grabCameraTarget;

        [Header("焦りへの追い込み")]
        [Tooltip(
            "プレイヤーの焦り（PanicDetector）に応じて攻めを強める設定。段階・系統ごとの補正値と歯止め。"
        )]
        [SerializeField]
        private TBossPanicPressureSettings panicPressureSettings = new TBossPanicPressureSettings();

        // 外部参照用
        public Transform FlashlightTransform => flashlightTransform;
        public Light FlashlightLight => flashlightLight;
        public float LostSightDuration => lostSightDuration;

        /// <summary>プレイヤーが光から外れてから見失うまでの時間（秒）。焦りへの追い込み（逃げ腰）で長くなる。</summary>
        public float EffectiveLostSightDuration =>
            lostSightDuration * PanicPressure.LostSightDurationScale;
        public float CombatCloseSenseRadius => combatCloseSenseRadius;

        /// <summary>
        /// ボス本体の拡大率。ステート内の固定距離（到着判定・徘徊半径など）はスケール1の体格で調整した値なので、
        /// これを掛けて体格に合わせる。Prefabのスケール変更で行動が破綻しないようにするため。
        /// </summary>
        public float BodyScale => transform.lossyScale.y;

        public TrailRenderer ScytheTrail => scytheTrail;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float StrafeSpeed => strafeSpeed;
        public float AttackRange => attackRange;
        public float Attack1StrikeDistance => attack1StrikeDistance;
        public float Attack1MaxStepIn => attack1MaxStepIn;
        public float NormalAttack2Range => normalAttack2Range;
        public float SpecialAttackRange => specialAttackRange;
        public float WatchDistanceMin => watchDistanceMin;
        public float WatchDistanceMax => watchDistanceMax;
        public float DirectAssaultChance => directAssaultChance;

        /// <summary>
        /// 追跡で間合いに入った時、様子見を挟まず突っ込む確率（焦りへの追い込みで補正済み）。
        /// 追い込み開始の合図の直後は、プレイヤーが身構える間を取るため突っ込まない。
        /// </summary>
        public float EffectiveDirectAssaultChance =>
            PanicPressure.IsInCueLead
                ? 0f
                : PanicPressure.AdjustDirectAssaultChance(directAssaultChance);
        public float SpecialAttackChance => specialAttackChance;
        public float NormalAttack2Chance => normalAttack2Chance;
        public float NormalAttackCooldown => normalAttackCooldown;
        public float NormalAttack2Cooldown => normalAttack2Cooldown;
        public float SpecialAttackCooldown => specialAttackCooldown;
        public Transform WireBone => wireBone;
        public Rig WireRig => wireRig;
        public Transform WireIkTarget => wireIkTarget;
        public float SpecialAttackAimDuration => specialAttackAimDuration;
        public float SpecialAttackShootDuration => specialAttackShootDuration;
        public bool ForceStraightWireByRig => forceStraightWireByRig;
        public float WireHitRadius => wireHitRadius;

        /// <summary>
        /// 音が聞こえたかと、その確信度（0〜1）を判定する。聞こえる範囲は鳴らした側が決めた音の半径（足音・ハンマーなど）で、
        /// 音源がその半径の端なら確信度0、すぐそばなら1。大きな音ほど遠くから聞こえ、遠くでも確信度が残る。
        /// 以前はボス側の聴覚半径でも上限を掛けていたため、音の半径を広げても一定距離より遠いと聞こえなかった。
        /// </summary>
        public bool TryHearSound(SoundEventData data, out float confidence)
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
        public float GrabDamagePerTick => Status.CurrentAttackPower * grabDamageMultiplier;
        public float GrabDamageInterval => grabDamageInterval;
        public float GrabEscapeThreshold => grabEscapeThreshold;
        public float GrabEscapePerInput => grabEscapePerInput;
        public float GrabPullDuration => grabPullDuration;
        public float GrabPullDistance => grabPullDistance;
        public float GrabPullLateralOffset => grabPullLateralOffset;
        public ParticleSystem ElectricEffectPrefab => electricEffectPrefab;
        public float GrabRetractDuration => grabRetractDuration;
        public Transform GrabCameraTarget => grabCameraTarget;
        public Transform KamaBone => kamaBone;
        public Vector3 KamaExtendLocalAxis => kamaExtendLocalAxis;
        public float Attack2ReachExtension => attack2ReachExtension;
        public float Attack2ReachHoldAnimatorSpeed => attack2ReachHoldAnimatorSpeed;
        public float Attack2ReachHoldDuration => attack2ReachHoldDuration;
        public float Attack2ReachLerpDuration => attack2ReachLerpDuration;
        public float Attack2HomingRotateSpeed => attack2HomingRotateSpeed;
        public float Attack2PullDistance => attack2PullDistance;
        public float Attack2PullDuration => attack2PullDuration;
        public EnemyMeleeHitbox MeleeHitbox => meleeHitbox;

        /// <summary>ワイヤー側の腕の連鎖設定。Awakeが走らないEditモードのデバッグ用途でも参照できるよう公開する。</summary>
        public TBossLimbChainSettings WireChainSettings => wireChainSettings;

        /// <summary>鎌側の腕の連鎖設定。Awakeが走らないEditモードのデバッグ用途でも参照できるよう公開する。</summary>
        public TBossLimbChainSettings KamaChainSettings => kamaChainSettings;

        /// <summary>ワイヤー側の腕をしなやかに動かすドライバ。Awakeで生成される。</summary>
        public TBossLimbChainDriver WireChainDriver { get; private set; }

        /// <summary>鎌側の腕をしなやかに動かすドライバ。Awakeで生成される。</summary>
        public TBossLimbChainDriver KamaChainDriver { get; private set; }

        /// <summary>行動に合わせて鳴らす音。同じ GameObject の TutorialBossSounds をAwakeで取得する。</summary>
        public TutorialBossSounds Sounds { get; private set; }

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

        /// <summary>
        /// TransitionToAttackが選択しうる攻撃ステートの種別。
        /// </summary>
        private enum TBossAttackType
        {
            Normal,
            Normal2,
            Special,
        }

        // 視界判定・発見度（Awareness）・既知プレイヤー位置の管理を専門に行うクラス。責務分離のため別クラスに切り出している。
        private TBossPerception _perception;

        /// <summary>地形・障害物から攻撃や直線接近が成立するかを判定する。Awakeで生成される。</summary>
        public TBossAttackFeasibility AttackFeasibility { get; private set; }

        // 交戦中の攻撃タイミング。追跡・様子見の往復で待ち時間がやり直しにならないよう、ステートではなくここで持つ
        private readonly TBossCombatTempo _combatTempo = new TBossCombatTempo();

        // 見渡しモーションの再生と、その間の足止め。Awake で生成する（Animator が必要なため）
        private TBossLookAround _lookAround;

        // 各ステートが要求している「NavMeshAgent を止めるか」。見渡し中はこれに関係なく止める
        private bool _agentStopRequested;

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

        // ステートが変わる時に捨てる、行動モーション用のトリガー（死亡は捨てない）
        private static readonly string[] ActionTriggers =
        {
            "Attack",
            "Attack2",
            "SpecialAttack",
            "Flinch",
            "Parried",
            "Look",
        };

        /// <summary>
        /// 前のステートが引いたトリガーが、遷移の途中などで使われずに残っていると、後で状況と無関係に再生されるため捨てる。
        /// 次のステートが必要なトリガーはその Enter で引き直す。
        /// </summary>
        protected override void OnBeforeStateEnter()
        {
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
            bool lookAllowed =
                currentState is TBossPatrolState
                || currentState is TBossSearchState
                || currentState is TBossSoundInvestigateState;
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

        /// <summary>今のステートの名前（デバッグ表示・ログ用）。</summary>
        public string CurrentStateName =>
            currentState != null ? currentState.GetType().Name : "なし";

        /// <summary>追跡・様子見に入った時に呼ぶ。交戦の時計がまだ動いていなければ動かし始める。</summary>
        public void BeginCombatTempo() => _combatTempo.EnsureScheduled(CurrentAttackInterval);

        /// <summary>攻撃と攻撃の間隔の基準（秒）。焦りへの追い込み（短く）・息継ぎ（長く）で補正済み。</summary>
        private float CurrentAttackInterval => watchDuration * PanicPressure.AttackIntervalScale;

        /// <summary>交戦が終わった時（巡回へ戻る等）に呼ぶ。次の交戦では待ち時間を新しく決める。</summary>
        public void EndCombatTempo() => _combatTempo.Clear();

        private PlayerStatus _playerStatus;

        // プレイヤーの焦り度。無いプレイヤー（焦り度を持たないシーン）でもボスは追い込みなしで動く
        private PanicDetector _playerPanic;

        /// <summary>プレイヤーの焦りに応じた追い込みの判断。Awakeで生成される。</summary>
        public TBossPanicPressure PanicPressure { get; private set; }

        // 追い込み開始の合図（目の光）。無ければ音だけで合図する
        private TBossEyeGlint _eyeGlint;

        // プレイヤー死亡時の交戦終了を一度だけ行うためのフラグ（復活した場合は解除する）
        private bool _hasDisengagedFromDeadPlayer;

        // 今の掴みを、追い込みの歯止めの命中として数え済みか（掴みが終わったら戻す）
        private bool _hasCountedCurrentCapture;

        /// <summary>プレイヤーが倒れているか。倒れたプレイヤーは知覚・攻撃の対象にしない。</summary>
        public bool IsPlayerDead => _playerStatus != null && _playerStatus.IsDead;

        /// <summary>
        /// 顔（と頭に付けた懐中電灯）をプレイヤーへ向ける状態か。追跡・様子見の間と、巡回中に怪しんで見つめている間だけ true。
        /// 攻撃・怯み・捕獲・未発見時はアニメーションに任せ、攻撃後の硬直中に視界から抜ける逃げ道を残す。
        /// </summary>
        public bool IsHeadAimActive =>
            currentState is TBossChaseState
            || currentState is TBossWatchState
            || (currentState is TBossPatrolState && IsSuspicious);

        /// <summary>
        /// 通常攻撃・通常攻撃2（鎌）・特殊攻撃（触手）のステート中か。走り込みの接近フェーズも含む。
        /// 捕獲は攻撃が当たった後の別ステートなので含めない。
        /// </summary>
        public override bool IsAttacking =>
            currentState
                is TBossNormalAttackState
                    or TBossNormalAttack2State
                    or TBossSpecialAttackState;

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

            if (kamaBone != null)
            {
                InitialKamaBoneLocalPosition = kamaBone.localPosition;
                InitialKamaBoneLocalRotation = kamaBone.localRotation;
            }

            WireChainDriver = new TBossLimbChainDriver(wireChainSettings);
            KamaChainDriver = new TBossLimbChainDriver(kamaChainSettings);

            _perception = new TBossPerception(this);
            _lookAround = new TBossLookAround(Animator);
            AttackFeasibility = new TBossAttackFeasibility(this, _perception);

            if (Player != null)
            {
                _playerStatus = Player.GetComponent<PlayerStatus>();
                Assert.IsNotNull(_playerStatus, $"{name}: プレイヤーに PlayerStatus がありません");

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

            _eyeGlint = GetComponent<TBossEyeGlint>();
            if (_eyeGlint == null)
            {
                Debug.LogWarning(
                    $"{name}: TBossEyeGlint が無いため、追い込み開始の合図は音だけになります"
                );
            }

            Sounds = GetComponent<TutorialBossSounds>();
            Assert.IsNotNull(Sounds, $"{name}: TutorialBossSounds が同じ GameObject にありません");
        }

        protected override void Start()
        {
            base.Start();
            HideScytheTrail();
            ChangeState(new TBossPatrolState(this));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_playerStatus != null)
            {
                _playerStatus.Damaged += OnPlayerDamaged;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_playerStatus != null)
            {
                _playerStatus.Damaged -= OnPlayerDamaged;
            }
        }

        /// <summary>
        /// プレイヤーが被弾した時に呼ばれる。このボスの攻撃・掴みによる被弾だけを、追い込みの歯止め（命中数）に数える。
        /// 掴み中の電撃は一定間隔の継続ダメージなので、掴み1回を1命中として数える。
        /// </summary>
        private void OnPlayerDamaged()
        {
            if (currentState is TBossCapturedState)
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
            _perception.Tick(Time.deltaTime);
            TickPanicPressure();

            if (currentState is not TBossCapturedState)
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
                && currentState is not TBossDeathState;
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
            Debug.Log($"[TutorialBoss] 追い込み開始（{category.ToDisplayName()}）");

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
            Debug.Log($"[TutorialBoss] 追い込み終了（{PanicPressure.Mode}）");

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

            if (_hasDisengagedFromDeadPlayer || currentState is TBossDeathState)
            {
                return;
            }

            _hasDisengagedFromDeadPlayer = true;
            Debug.Log("[TutorialBoss] プレイヤーが倒れたため交戦を終了");
            IsAlerted = false;
            _perception.ForgetPlayer();
            ChangeState(new TBossPatrolState(this));
        }

        /// <summary>
        /// 鎌のTrailの描画を開始するAnimationEvent用関数。emittingによる描画制御を用いている。
        /// </summary>
        public void ShowScytheTrail()
        {
            scytheTrail.emitting = true;
        }

        /// <summary>
        /// 鎌のTrailの描画を停止するAnimationEvent用関数。emittingによる描画制御を用いている。
        /// </summary>
        public void HideScytheTrail()
        {
            scytheTrail.emitting = false;
        }

        // プレイヤーとの衝突を戻す予約。重なったまま戻すと、プレイヤーがめり込みから押し出されて上へ飛ぶため、離れるまで待つ
        private bool _isPlayerCollisionRestorePending;

        // 「離れた」とみなすための、体が触れる距離に足す余裕（m）
        private const float SeparationMargin = 0.1f;

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
        /// 攻撃の振りが途中で打ち切られた時の後始末。振り終わりのAnimationEventが呼ばれないため、
        /// 攻撃判定と鎌の軌跡が出たまま残らないよう、ここで止める。
        /// </summary>
        public void StopSwingEffects()
        {
            if (meleeHitbox != null)
            {
                meleeHitbox.DisableHitbox();
            }
            HideScytheTrail();
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
            Animator.SetFloat("VelocityX", localVelocity.x);
            Animator.SetFloat("VelocityZ", localVelocity.z);
            UpdateRunAnimation();
        }

        // 走りモーションに切り替える／戻す速度を、歩き速度と走り速度の間のどこに置くか（0=歩き速度、1=走り速度）。
        // 入りと戻りの閾値をずらし、閾値付近の速度で走り⇔歩きがチラつくのを防ぐ。
        private const float RunAnimationEnterSpeedRatio = 0.6f;
        private const float RunAnimationExitSpeedRatio = 0.4f;

        private bool _isRunAnimationActive;

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
                walkSpeed,
                runSpeed,
                _isRunAnimationActive ? RunAnimationExitSpeedRatio : RunAnimationEnterSpeedRatio
            );
            // isStopped は NavMesh 上に無いエージェントで参照するとエラーになるため isOnNavMesh を先に見る
            bool shouldRun = Agent.isOnNavMesh && !Agent.isStopped && speed >= threshold;

            if (shouldRun != _isRunAnimationActive)
            {
                _isRunAnimationActive = shouldRun;
                Animator.SetBool("IsRunning", shouldRun);
            }
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();
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
            if (IsCommittedToAction() || Player == null || IsPlayerDead)
            {
                return;
            }

            IsAlerted = true;

            if (CheckPlayerTracked())
            {
                // 見えているなら交戦する。既に追跡・様子見中ならそのまま続ける
                if (!(currentState is TBossChaseState) && !(currentState is TBossWatchState))
                {
                    ChangeState(new TBossChaseState(this));
                }
                return;
            }

            // 光の外・背後・物陰から撃たれた。EnemyStatusは攻撃者の座標を渡してこないため、
            // 現在のプレイヤー位置を推定攻撃元として既知位置に入れ、追跡（見失い中の挙動＝そこへ走る）で向かわせる
            _perception.NotifyDamageFrom(Player.transform.position);
            ChangeState(new TBossChaseState(this));
        }

        /// <summary>被弾や音で行動を差し替えてはいけない、やり切るべき行動の最中か。</summary>
        private bool IsCommittedToAction()
        {
            return currentState is TBossNormalAttackState
                || currentState is TBossNormalAttack2State
                || currentState is TBossSpecialAttackState
                || currentState is TBossCapturedState
                || currentState is TBossFlinchState
                || currentState is TBossParriedState
                || currentState is TBossDeathState;
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
            if (currentState is TBossDeathState)
            {
                return;
            }
            ChangeState(new TBossParriedState(this));
        }

        // ────────────────────────────────────────────
        //  視界判定・知覚（TBossPerceptionへの窓口）
        // ────────────────────────────────────────────

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐（SpotAngle・Range）内にいるかを判定する。
        /// 障害物による遮蔽も考慮する。実体はTBossPerceptionが持つ。
        /// </summary>
        public bool CheckInFlashlight() => _perception.IsPlayerVisible();

        /// <summary>
        /// 交戦中（追跡・様子見）の捕捉判定。光円錐内に加え、CombatCloseSenseRadius以内で見通せれば捕捉中とみなす。
        /// </summary>
        public bool CheckPlayerTracked() => _perception.IsPlayerTracked();

        /// <summary>発見度（0〜100）。継続して視認するほど上がり、光の外では時間経過で下がる。</summary>
        public float Awareness => _perception.Awareness;

        /// <summary>発見度が上限に達し、完全にプレイヤーの存在を把握した状態か。</summary>
        public bool IsFullyAware => _perception.IsFullyAware;

        /// <summary>ちらっと見えた・音が聞こえた等で「怪しい」と感じている状態か。</summary>
        public bool IsSuspicious => _perception.IsSuspicious;

        /// <summary>最後に把握したプレイヤーの位置（視認・被弾などから更新）。</summary>
        public Vector3 LastKnownPlayerPosition => _perception.LastKnownPlayerPosition;

        /// <summary>一度でもプレイヤーの位置を把握したことがあるか。</summary>
        public bool HasKnownPlayerPosition => _perception.HasKnownPlayerPosition;

        /// <summary>音を検知した際に発見度へ反映する。confidenceは0〜1（音源に近いほど高い）。</summary>
        public void NotifySoundHeard(float confidence) => _perception.NotifySoundHeard(confidence);

        /// <summary>プレイヤーの位置で鳴る音（足音など）を聞いた際に、既知位置を音源へ更新する。</summary>
        public void NotifyPlayerHeardAt(Vector3 position) =>
            _perception.NotifyPlayerHeardAt(position);

        /// <summary>見失ったプレイヤーが逃げた先の予測地点（最後に見た位置と移動方向から求める）。</summary>
        public Vector3 PredictPlayerPosition() => _perception.PredictPlayerPosition();

        /// <summary>
        /// 特殊攻撃のアニメーション中、一番ワイヤーを射出するのに適したポーズで呼ばれるAnimationEvent。
        /// </summary>
        public void OnSpecialAttackPoseReady()
        {
            // 特殊攻撃ステートの間だけ構えで一時停止する。ステートを抜けた後にモーションの残りでこのイベントが来ても、
            // Animator 全体を止めてボスを棒立ちにしないため
            if (currentState is TBossSpecialAttackState specialState)
            {
                if (Animator != null)
                {
                    Animator.speed = 0f;
                }
                specialState.OnPoseReady();
            }
        }

        /// <summary>
        /// 通常攻撃2のアニメーション中、鎌のリーチを伸ばし始める箇所で呼ばれるAnimationEvent。
        /// 各振りの直前に置く。1クリップ内で複数回呼んでよい。
        /// </summary>
        public void OnAttack2ReachExtend()
        {
            if (currentState is TBossNormalAttack2State attack2State)
            {
                attack2State.OnReachExtend();
            }
        }

        /// <summary>
        /// 通常攻撃2のアニメーション中、鎌のリーチを元の長さへ戻し始める箇所で呼ばれるAnimationEvent。
        /// 振り切った直後に置く。OnAttack2ReachExtendと対で、1クリップ内で複数回呼んでよい。
        /// </summary>
        public void OnAttack2ReachRetract()
        {
            if (currentState is TBossNormalAttack2State attack2State)
            {
                attack2State.OnReachRetract();
            }
        }

        /// <summary>
        /// 通常攻撃2のアニメーション中、鎌のリーチを即座に元の長さへ戻すAnimationEvent。
        /// 補間を待たないため、クリップ終端で確実に元の長さへ戻したい場合に置く。
        /// </summary>
        public void OnAttack2ReachReset()
        {
            if (currentState is TBossNormalAttack2State attack2State)
            {
                attack2State.OnReachReset();
            }
        }

        // 同じ攻撃が何回連続で選ばれたら次は避けるか（3回目の選択をブロックする）。
        private const int MaxAttackRepeatBeforeAvoid = 2;

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
        public bool TryStartAttack()
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
                meleeHeightOk
                && distance <= Mathf.Max(attackRange, meleeChargeRange)
                && Time.time >= _normalAttackReadyTime;
            bool normal2Eligible =
                meleeHeightOk
                && distance <= Mathf.Max(normalAttack2Range, meleeChargeRange)
                && Time.time >= _normalAttack2ReadyTime
                && (distance > normalAttack2Range || AttackFeasibility.HasMeleeLine());
            // 特殊攻撃（触手の拘束）は至近距離で使うと不自然なため、通常攻撃の射程より外側でのみ選択可能にする。
            // 空振りすると長いクールダウンを無駄にするため、射線が通っている時だけ選ぶ（重い判定なので最後に評価）
            bool specialEligible =
                distance > attackRange
                && distance <= specialAttackRange
                && Time.time >= _specialAttackReadyTime
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
            IsAlerted = true;
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
                ? specialAttackChance * PanicPressure.SpecialWeightScale
                : 0f;
            float normal2Weight = normal2Eligible
                ? normalAttack2Chance * PanicPressure.Normal2WeightScale
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
                    _specialAttackReadyTime = Time.time + specialAttackCooldown;
                    ChangeState(new TBossSpecialAttackState(this));
                    break;
                case TBossAttackType.Normal2:
                    _normalAttack2ReadyTime = Time.time + normalAttack2Cooldown;
                    ChangeState(new TBossNormalAttack2State(this));
                    break;
                default:
                    _normalAttackReadyTime = Time.time + normalAttackCooldown;
                    ChangeState(new TBossNormalAttackState(this));
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 攻撃射程
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 通常攻撃2射程
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, normalAttack2Range);

            // 特殊攻撃射程
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, specialAttackRange);

            // 交戦中に光の外でも捕捉し続ける距離
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, combatCloseSenseRadius);
        }
#endif
    }
}
