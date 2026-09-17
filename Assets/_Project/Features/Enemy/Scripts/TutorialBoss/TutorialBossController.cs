using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

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

        [Header("タイマー設定")]
        [Tooltip("様子見ステートの継続時間（秒）。")]
        [SerializeField]
        private float watchDuration = 2f;

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

        [Header("音反応設定")]
        [Tooltip("反応する音の最大半径フィルタ（未実装の半径フィルタ用）。")]
        [SerializeField]
        private float soundReactRadius = 15f;

        [Header("障害物設定")]
        [SerializeField]
        private LayerMask obstacleLayer;

        [Header("掴み（捕獲）設定")]
        [Tooltip("電撃ダメージ（1ティックあたり）。")]
        [SerializeField]
        private float grabDamagePerTick = 5f;

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

        [Tooltip("脱出後にプレイヤーを後方へ押し出す力。")]
        [SerializeField]
        private float grabEscapeKnockbackForce = 8f;

        [Tooltip("電撃エフェクトの ParticleSystem プレハブ（プレイヤーにアタッチして使用）。")]
        [SerializeField]
        private ParticleSystem electricEffectPrefab;

        [Tooltip("触手の引っ込み演出にかかる時間（秒）。捕獲脱出後のリトラクト。")]
        [SerializeField]
        private float grabRetractDuration = 0.4f;

        // 外部参照用
        public Transform FlashlightTransform => flashlightTransform;
        public Light FlashlightLight => flashlightLight;
        public float LostSightDuration => lostSightDuration;

        public TrailRenderer ScytheTrail => scytheTrail;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float StrafeSpeed => strafeSpeed;
        public float AttackRange => attackRange;
        public float NormalAttack2Range => normalAttack2Range;
        public float SpecialAttackRange => specialAttackRange;
        public float WatchDuration => watchDuration;
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
        public float SoundReactRadius => soundReactRadius;
        public LayerMask ObstacleLayer => obstacleLayer;
        public float GrabDamagePerTick => grabDamagePerTick;
        public float GrabDamageInterval => grabDamageInterval;
        public float GrabEscapeThreshold => grabEscapeThreshold;
        public float GrabEscapePerInput => grabEscapePerInput;
        public float GrabPullDuration => grabPullDuration;
        public float GrabPullDistance => grabPullDistance;
        public float GrabPullLateralOffset => grabPullLateralOffset;
        public float GrabEscapeKnockbackForce => grabEscapeKnockbackForce;
        public ParticleSystem ElectricEffectPrefab => electricEffectPrefab;
        public float GrabRetractDuration => grabRetractDuration;
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();
            UpdateAnimatorParameters();
            _perception.Tick(Time.deltaTime);
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
        }

        public override void ForceFlinch()
        {
            base.ForceFlinch();
            ChangeState(new TBossFlinchState(this));
        }

        public override void ForceAlert()
        {
            base.ForceAlert();
            if (IsAlerted)
            {
                return;
            }

            IsAlerted = true;

            if (CheckInFlashlight())
            {
                // 被弾した瞬間にプレイヤーが見えているならそのまま追跡できる
                ChangeState(new TBossChaseState(this));
                return;
            }

            // 光の外・背後などから被弾したケース。EnemyStatusは攻撃者の座標を渡してこないため、
            // 既知のプレイヤー位置があればそこへ、無ければ現在のプレイヤー位置を推定攻撃元として代用する。
            Vector3 searchTarget = _perception.HasKnownPlayerPosition
                ? _perception.LastKnownPlayerPosition
                : (Player != null ? Player.transform.position : transform.position);

            if (Player != null)
            {
                _perception.NotifyDamageFrom(Player.transform.position);
            }

            ChangeState(new TBossSearchState(this, searchTarget));
        }

        public override void ForceDeath()
        {
            base.ForceDeath();
            ChangeState(new TBossDeathState(this));
        }

        // ────────────────────────────────────────────
        //  視界判定・知覚（TBossPerceptionへの窓口）
        // ────────────────────────────────────────────

        /// <summary>
        /// プレイヤーが懐中電灯の光円錐（SpotAngle・Range）内にいるかを判定する。
        /// 障害物による遮蔽も考慮する。実体はTBossPerceptionが持つ。
        /// </summary>
        public bool CheckInFlashlight() => _perception.IsPlayerVisible();

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

        /// <summary>
        /// 特殊攻撃のアニメーション中、一番ワイヤーを射出するのに適したポーズで呼ばれるAnimationEvent。
        /// </summary>
        public void OnSpecialAttackPoseReady()
        {
            if (Animator != null)
            {
                Animator.speed = 0f; // アニメーションを一時停止
            }

            if (currentState is TBossSpecialAttackState specialState)
            {
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

        // 通常攻撃は接近フェーズを持ち多少の距離超過を追いかけて詰められるため、
        // 射程判定にこの倍率分の猶予を持たせて選択可能にする。
        private const float NormalAttackRangeTolerance = 1.5f;

        // 同じ攻撃が何回連続で選ばれたら次は避けるか（3回目の選択をブロックする）。
        private const int MaxAttackRepeatBeforeAvoid = 2;

        /// <summary>
        /// プレイヤーとの距離・各攻撃のクールダウン・直前の選択履歴から攻撃ステートを選び遷移する。
        /// どの攻撃も間合いに合っていない場合は攻撃せずChaseへ戻して間合いを詰め直す。
        /// </summary>
        public void TransitionToAttack()
        {
            // 攻撃に入る時点で警戒状態を確定させる。
            // EnemyStatusは被弾ごとにOnAlertTriggeredを無条件発火するため、IsAlertedがfalseのままだと
            // ForceAlertが走って攻撃モーションや捕獲シーケンスが強制的にChaseへ差し替えられてしまう。
            IsAlerted = true;

            if (Player == null)
            {
                ChangeState(new TBossWatchState(this));
                return;
            }

            float distance = Vector3.Distance(transform.position, Player.transform.position);

            bool normalEligible =
                distance <= attackRange * NormalAttackRangeTolerance
                && Time.time >= _normalAttackReadyTime;
            bool normal2Eligible =
                distance <= normalAttack2Range && Time.time >= _normalAttack2ReadyTime;
            // 特殊攻撃（触手の拘束）は至近距離で使うと不自然なため、通常攻撃の射程より外側でのみ選択可能にする
            bool specialEligible =
                distance > attackRange
                && distance <= specialAttackRange
                && Time.time >= _specialAttackReadyTime;

            SuppressRepeatedChoice(ref normalEligible, ref normal2Eligible, ref specialEligible);

            if (!normalEligible && !normal2Eligible && !specialEligible)
            {
                // 間合いが合っていない、または全てクールダウン中。距離を詰め直す。
                ChangeState(new TBossChaseState(this));
                return;
            }

            CommitAttack(ChooseWeightedAttack(normalEligible, normal2Eligible, specialEligible));
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
        /// </summary>
        private TBossAttackType ChooseWeightedAttack(
            bool normalEligible,
            bool normal2Eligible,
            bool specialEligible
        )
        {
            float specialWeight = specialEligible ? specialAttackChance : 0f;
            float normal2Weight = normal2Eligible ? normalAttack2Chance : 0f;
            float normalWeight = normalEligible ? 1f : 0f;

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

            // 音反応半径
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, soundReactRadius);
        }
#endif
    }
}
