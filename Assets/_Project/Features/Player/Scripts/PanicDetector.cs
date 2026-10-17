using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの「焦り」を複数の行動指標から検知し、0〜100の焦り度スコアとして管理するコンポーネント。
    /// 現時点では敵の行動には影響せず、デバッグUI（PanicDebugUI）への情報提供のみを目的とする。
    ///
    /// このクラスの役割は、毎フレームの観測値（<see cref="PanicContext"/>）の取りまとめ、
    /// 入力・被弾イベントの各指標への受け渡し、結果の公開に限る。
    /// 各指標の判定は Panic フォルダの <see cref="PanicSignal"/> 派生クラス、合算は <see cref="PanicScoreCalculator"/> が持つ。
    ///
    /// 検知指標:
    ///   1. 逃避     <see cref="FlightSignal"/>
    ///   2. スパム   <see cref="GuardSpamSignal"/>
    ///   3. ジッター <see cref="AimJitterSignal"/>
    ///   4. 空間喪失 <see cref="TrapSignal"/>
    ///   5. 意思崩壊 <see cref="AimCancelSignal"/>
    ///   6. 回避失敗 <see cref="HitChainSignal"/>
    ///
    /// 回避失敗以外の指標は、交戦中の敵がいる間（<see cref="IsInCombat"/>）だけ計測する。
    /// 気付かれていない隠密行動中に背を向けて移動したり、構えて解いたりするのは焦りではないため。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerStatus))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerCameraController))]
    [RequireComponent(typeof(PlayerTargetManager))]
    [RequireComponent(typeof(WeaponManager))]
    public class PanicDetector : MonoBehaviour
    {
        // 同じリグ上のコンポーネント(Awake で取得)
        private PlayerController _playerController;
        private PlayerInputHandler _inputHandler;
        private BowController _bowController;
        private PlayerStatus _playerStatus;
        private CharacterController _characterController;
        private PlayerCameraController _camera;
        private PlayerTargetManager _targetManager;
        private WeaponManager _weaponManager;

        [Header("交戦相手")]
        [SerializeField]
        private PanicEngagementTracker _engagement = new PanicEngagementTracker();

        [Header("逃避")]
        [SerializeField]
        private FlightSignal _flight = new FlightSignal();

        [Header("スパム")]
        [SerializeField]
        private GuardSpamSignal _guardSpam = new GuardSpamSignal();

        [Header("ジッター")]
        [SerializeField]
        private AimJitterSignal _aimJitter = new AimJitterSignal();

        [Header("空間喪失")]
        [SerializeField]
        private TrapSignal _trap = new TrapSignal();

        [Header("意思崩壊")]
        [SerializeField]
        private AimCancelSignal _aimCancel = new AimCancelSignal();

        [Header("回避失敗")]
        [SerializeField]
        private HitChainSignal _hitChain = new HitChainSignal();

        [Header("総合焦り度")]
        [SerializeField]
        private PanicScoreCalculator _scoreCalculator = new PanicScoreCalculator();

        private readonly PanicContext _context = new PanicContext();
        private PanicSignal[] _signals;

        /// <summary>総合焦り度スコア（0〜100）</summary>
        public float PanicScore => _scoreCalculator.Score;

        /// <summary>交戦中の敵がいるか。回避失敗以外の指標はこれが true の間だけ計測する。</summary>
        public bool IsInCombat => _context.IsInCombat;

        /// <summary>今フレームの観測値（交戦相手・距離・ボスが攻撃中かなど）。読み取り専用で使う。</summary>
        public PanicContext Context => _context;

        /// <summary>逃避検知が有効かつ、交戦中の敵がいるか</summary>
        public bool IsFlightDetectionActive => _flight.IsEnabled && _context.IsInCombat;

        /// <summary>全指標（表示順）。</summary>
        public IReadOnlyList<PanicSignal> Signals => _signals;

        public FlightSignal Flight => _flight;
        public GuardSpamSignal GuardSpam => _guardSpam;
        public AimJitterSignal AimJitter => _aimJitter;
        public TrapSignal Trap => _trap;
        public AimCancelSignal AimCancel => _aimCancel;
        public HitChainSignal HitChain => _hitChain;

        /// <summary>最後に検知されたシグナル名</summary>
        public string LastDetectedSignal { get; private set; } = "なし";

        /// <summary>
        /// 焦りシグナルを検知したとき発火するイベント。引数は検知した指標の名前。
        /// 継続する指標（逃避・ジッター・空間喪失）は、兆候が出始めた瞬間に1回だけ発火する。
        /// </summary>
        public event Action<string> OnPanicSignalDetected;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _inputHandler = GetComponent<PlayerInputHandler>();
            _characterController = GetComponent<CharacterController>();
            _playerStatus = GetComponent<PlayerStatus>();
            _camera = GetComponent<PlayerCameraController>();
            _targetManager = GetComponent<PlayerTargetManager>();
            _weaponManager = GetComponent<WeaponManager>();
            _bowController = GetComponentInChildren<BowController>(includeInactive: true);
            Assert.IsNotNull(_bowController, $"{name}: 子階層に BowController が見つかりません");

            _signals = new PanicSignal[] { _flight, _guardSpam, _aimJitter, _trap, _aimCancel, _hitChain };
        }

        private void OnEnable()
        {
            _inputHandler.OnSubActionPressed += OnDefenseButtonPressed;
            // HPの増減ではなく被弾そのものを購読する(最大HP低下によるHPの切り詰めを被弾と誤認しないため)
            _playerStatus.Damaged += OnPlayerDamaged;
            _bowController.OnAimCancelled += OnAimCancelled;
            foreach (PanicSignal signal in _signals)
            {
                signal.Detected += OnSignalDetected;
            }
        }

        private void OnDisable()
        {
            _inputHandler.OnSubActionPressed -= OnDefenseButtonPressed;
            _playerStatus.Damaged -= OnPlayerDamaged;
            _bowController.OnAimCancelled -= OnAimCancelled;
            foreach (PanicSignal signal in _signals)
            {
                signal.Detected -= OnSignalDetected;
            }
        }

        private void Update()
        {
            RefreshContext();

            float deltaTime = Time.deltaTime;
            foreach (PanicSignal signal in _signals)
            {
                signal.Tick(_context, deltaTime);
            }

            _scoreCalculator.Update(_signals, deltaTime);
        }

        /// <summary>
        /// 外部やイベントから逃避判定の有効/無効を切り替えるメソッド
        /// </summary>
        public void SetFlightDetectionEnabled(bool enabled)
        {
            _flight.SetEnabled(enabled);
        }

        /// <summary>
        /// 敵が死亡したり戦闘が終了したときなど、外部からリセットを呼べる公開メソッド。
        /// </summary>
        public void ResetAllScores()
        {
            foreach (PanicSignal signal in _signals)
            {
                signal.ResetState();
            }
            _scoreCalculator.Reset();
            _engagement.Clear();
            LastDetectedSignal = "なし";
        }

        /// <summary>今フレームの交戦状況とプレイヤーの状態を観測値に詰める。</summary>
        private void RefreshContext()
        {
            Vector3 playerPosition = transform.position;
            _engagement.Refresh(_targetManager.CurrentTargetCollider, playerPosition);

            EnemyBaseController enemy = _engagement.EngagedEnemy;
            _context.EngagedEnemy = enemy;
            if (enemy != null)
            {
                Vector3 toEnemy = enemy.transform.position - playerPosition;
                toEnemy.y = 0f;
                _context.EnemyDistance = toEnemy.magnitude;
                _context.ToEnemyDirectionXZ = toEnemy.normalized;
            }
            else
            {
                _context.EnemyDistance = 0f;
                _context.ToEnemyDirectionXZ = Vector3.zero;
            }

            Vector3 velocity = _characterController.velocity;
            Vector3 cameraForward = Camera.main.transform.forward;

            _context.MoveInput = _inputHandler.move;
            _context.HorizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            _context.CanMove = _playerController.CanMove;
            _context.IsAiming = _bowController.IsAiming;
            _context.CameraForwardXZ = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
            _context.CameraYaw = _camera.CinemachineCameraTarget.transform.eulerAngles.y;
        }

        /// <summary>
        /// 防御ボタンが新たに押されたときに PlayerInputHandler から呼ばれる。
        /// 交戦中に剣を持っているときのみスパムとしてカウントする。
        /// </summary>
        private void OnDefenseButtonPressed()
        {
            if (!_context.IsInCombat)
                return;

            // subAction は武器ごとに意味が違う(剣=防御 / 弓=構え / 鎌=投擲)。
            // 「防御の連打」を測る指標なので、剣を装備しているときだけ数える
            GameObject weapon = _weaponManager.CurrentWeaponObject;
            if (weapon == null || !weapon.TryGetComponent<SwordController>(out _))
                return;

            _guardSpam.RecordPress();
        }

        /// <summary>
        /// プレイヤーが自分で弓の構えを解いた時に BowController から呼ばれる。
        /// 怯み・掴み・武器切替による強制解除は BowController 側で通知されないため数えない。
        /// </summary>
        private void OnAimCancelled()
        {
            if (!_context.IsInCombat)
                return;

            _aimCancel.RecordCancel();
        }

        /// <summary>
        /// プレイヤーが被弾したときに PlayerStatus から呼ばれる。
        /// </summary>
        private void OnPlayerDamaged()
        {
            // 掴まれている間の電撃は一定間隔の継続ダメージで、避ける余地がないため回避失敗に数えない
            if (_playerController.IsGrabbed)
                return;

            _hitChain.RecordHit();
        }

        private void OnSignalDetected(PanicSignal signal)
        {
            LastDetectedSignal = signal.DisplayName;
            OnPanicSignalDetected?.Invoke(signal.DisplayName);
        }
    }
}
