using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの「焦り」を複数の行動指標から検知し、0〜100の焦り度スコアとして管理するコンポーネント。
    /// 現時点では敵の行動には影響せず、デバッグUI（PanicDebugUI）への情報提供のみを目的とする。
    ///
    /// 検知指標（研究根拠あり）:
    ///   1. 逃避        - 敵にカメラを向けず移動し続けた時間  [Yerkes-Dodson / BIS-BAS]
    ///   2. スパム      - 剣の防御ボタンの連打頻度 (回/秒)   [Christou&Bhatt 2012]
    ///   3. ジッター    - 弓構え中のカメラ角速度の急上昇     [LeDoux 1994]
    ///   4. 空間喪失    - 逃げ入力中に実際に動けない時間     [Lazarus&Folkman 1984]
    ///   5. 意思崩壊    - 弓を構えたのに撃たずキャンセル累計 [Starcke&Brand 2012]
    ///   6. 回避失敗    - 短時間に連続被弾した回数           [Yerkes-Dodson 注意狭隘化]
    /// </summary>
    public class PanicDetector : MonoBehaviour
    {
        [Header("参照（自動取得。手動設定も可）")]
        [SerializeField]
        private PlayerController _playerController;

        [SerializeField]
        private PlayerInputHandler _inputHandler;

        [SerializeField]
        private BowController _bowController;

        [SerializeField]
        private PlayerStatus _playerStatus;

        [SerializeField]
        private CharacterController _characterController;

        [Tooltip("敵オブジェクトへの参照（逃避検知に使用）")]
        [SerializeField]
        private Transform _enemyTransform;

        [Header("逃避 設定")]
        [Tooltip("敵が存在しない場合やイベント中に逃避感知を動的に有効/無効にするか")]
        [SerializeField]
        private bool _enableFlightDetection = true;

        [Tooltip("敵オブジェクトがHierarchy上で有効（activeInHierarchy）な時のみ逃避とみなすか")]
        [SerializeField]
        private bool _requireActiveEnemy = true;

        [Tooltip("敵の参照がInspectorで未設定の場合、Tag 'Enemy' で自動検索するか")]
        [SerializeField]
        private bool _autoSearchEnemyByTag = true;

        [Tooltip("この秒数以上、敵にカメラを向けず移動し続けたら満点（重み: 20）")]
        [SerializeField]
        private float _flightMaxDuration = 3f;

        [Tooltip("カメラが敵方向と何度以上離れていれば「敵を向いていない」と判定するか")]
        [SerializeField]
        private float _flightAwayAngleThreshold = 80f;

        [Header("スパム 設定")]
        [Tooltip("この頻度（回/秒）以上で防御ボタンを押したら満点（重み: 20）")]
        [SerializeField]
        private float _spamMaxRate = 3f;

        [Tooltip("スパム頻度計測のウィンドウ幅（秒）")]
        [SerializeField]
        private float _spamWindowDuration = 1.5f;

        [Header("ジッター 設定")]
        [Tooltip("弓構え中にこの角速度（°/秒）以上のカメラ移動があればジッター満点（重み: 15）")]
        [SerializeField]
        private float _jitterMaxAngularSpeed = 180f;

        [Header("空間喪失 設定")]
        [Tooltip("この秒数以上、移動入力ありで実際に動けていない状態が続いたら満点（重み: 20）")]
        [SerializeField]
        private float _trapMaxDuration = 3f;

        [Tooltip("「動けていない」と判定する速度の閾値（m/s）")]
        [SerializeField]
        private float _trapVelocityThreshold = 0.5f;

        [Header("意思崩壊 設定")]
        [Tooltip("この回数以上、弓をキャンセルしたら満点（重み: 15）")]
        [SerializeField]
        private int _aimCancelMaxCount = 3;

        [Header("回避失敗 設定")]
        [Tooltip("この秒数以内に連続被弾したらカウントを加算する判定ウィンドウ（秒）")]
        [SerializeField]
        private float _hitChainWindow = 3f;

        [Tooltip("この連続被弾数以上で満点（重み: 10）")]
        [SerializeField]
        private int _hitChainMaxCount = 2;

        [Header("焦り度 減衰設定")]
        [Tooltip("焦り度が毎秒どれだけ自然に減少するか（Inspectorで調整）")]
        [SerializeField]
        private float _panicDecayPerSecond = 5f;

        // 逃避
        private float _flightTimer = 0f;

        // スパム
        private int _spamPressCount = 0;
        private float _spamElapsed = 0f;

        // ジッター
        private float _prevCamYaw = 0f;
        private float _jitterAngularSpeed = 0f;

        // 空間喪失
        private float _trapTimer = 0f;

        // 意思崩壊
        private int _aimCancelCount = 0;
        private bool _wasAiming = false;

        // 回避失敗
        private float _lastHitTime = -999f;
        private int _hitChainCount = 0;
        private float _lastHp = -1f;

        // 意思崩壊バグ修正用フラグ
        // FireArrow() の直後は IsAiming が false になるが、それはキャンセルではなく発射成功のため除外する
        private bool _justFired = false;

        /// <summary>総合焦り度スコア（0〜100）</summary>
        public float PanicScore { get; private set; } = 0f;

        /// <summary>逃避の正規化スコア（0〜1）</summary>
        public float FlightScore => Mathf.Clamp01(_flightTimer / _flightMaxDuration);

        /// <summary>逃避継続時間（秒）</summary>
        public float FlightTimer => _flightTimer;

        /// <summary>逃避検知が有効かつ対象の敵が存在しているか</summary>
        public bool IsFlightDetectionActive =>
            _enableFlightDetection &&
            _enemyTransform != null &&
            (!_requireActiveEnemy || _enemyTransform.gameObject.activeInHierarchy);

        /// <summary>スパムの正規化スコア（0〜1）</summary>
        public float SpamScore { get; private set; } = 0f;

        /// <summary>現在のスパム頻度（回/秒）</summary>
        public float SpamRate { get; private set; } = 0f;

        /// <summary>ジッターの正規化スコア（0〜1）</summary>
        public float JitterScore { get; private set; } = 0f;

        /// <summary>現在のカメラ角速度（°/秒）</summary>
        public float JitterAngularSpeed => _jitterAngularSpeed;

        /// <summary>空間喪失の正規化スコア（0〜1）</summary>
        public float TrapScore => Mathf.Clamp01(_trapTimer / _trapMaxDuration);

        /// <summary>空間喪失の継続時間（秒）</summary>
        public float TrapTimer => _trapTimer;

        /// <summary>意思崩壊の正規化スコア（0〜1）</summary>
        public float AimCancelScore => Mathf.Clamp01((float)_aimCancelCount / _aimCancelMaxCount);

        /// <summary>弓キャンセル累計回数</summary>
        public int AimCancelCount => _aimCancelCount;

        /// <summary>回避失敗の正規化スコア（0〜1）（2回目以降の連続被弾からカウント増加）</summary>
        public float HitChainScore =>
            _hitChainCount <= 1 || _hitChainMaxCount <= 1
                ? 0f
                : Mathf.Clamp01((float)(_hitChainCount - 1) / (_hitChainMaxCount - 1));

        /// <summary>連続被弾回数</summary>
        public int HitChainCount => _hitChainCount;

        /// <summary>最後に検知されたシグナル名</summary>
        public string LastDetectedSignal { get; private set; } = "なし";

        /// <summary>
        /// 焦りシグナルを検知したとき発火するイベント。
        /// 引数は検知した指標の名前。
        /// </summary>
        public event Action<string> OnPanicSignalDetected;

        private void Awake()
        {
            // 同じGameObject上のコンポーネントを自動取得（手動設定されていない場合のフォールバック）
            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();
            if (_inputHandler == null)
                _inputHandler = GetComponent<PlayerInputHandler>();
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>();
            if (_playerStatus == null)
                _playerStatus = GetComponent<PlayerStatus>();
            if (_bowController == null)
                _bowController = GetComponentInChildren<BowController>(includeInactive: true);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
                _inputHandler.OnSubActionPressed += OnDefenseButtonPressed;

            if (_playerStatus != null)
                _playerStatus.OnHpChanged += OnPlayerHpChanged;

            // 発射成功イベントを購読し、意思崩壊の誤検知を防ぐ
            BowController.OnFired += OnArrowFired;
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
                _inputHandler.OnSubActionPressed -= OnDefenseButtonPressed;

            if (_playerStatus != null)
                _playerStatus.OnHpChanged -= OnPlayerHpChanged;

            BowController.OnFired -= OnArrowFired;
        }

        private void Start()
        {
            if (_playerController != null)
            {
                _prevCamYaw =
                    _playerController.CinemachineCameraTarget != null
                        ? _playerController.CinemachineCameraTarget.transform.eulerAngles.y
                        : 0f;
            }
        }

        private void Update()
        {
            DetectFlight();
            DetectSpam();
            DetectJitter();
            DetectTrap();
            DetectAimCancel();
            // 回避失敗はイベント駆動（OnPlayerHpChanged）のため Update では行わない

            // ヒットチェーンのリセット（最後の被弾から一定時間経過したら連鎖を切る）
            if (_hitChainCount > 0 && Time.time - _lastHitTime > _hitChainWindow)
            {
                _hitChainCount = 0;
            }

            CalculatePanicScore();
        }

        /// <summary>
        /// 外部やイベントからターゲットの敵を動的に設定するメソッド
        /// </summary>
        public void SetEnemyTarget(Transform enemy)
        {
            _enemyTransform = enemy;
        }

        /// <summary>
        /// 外部やイベントから逃避判定の有効/無効を切り替えるメソッド
        /// </summary>
        public void SetFlightDetectionEnabled(bool enabled)
        {
            _enableFlightDetection = enabled;
            if (!enabled)
            {
                _flightTimer = 0f;
            }
        }

        /// <summary>
        /// 指標1: 逃避
        /// 敵方向からカメラが大きく離れており、かつプレイヤーが移動中の時間を計測する。
        /// 敵が存在しない場面や非アクティブな場合は逃避判定を行わない。
        /// </summary>
        private void DetectFlight()
        {
            if (!_enableFlightDetection)
            {
                _flightTimer = 0f;
                return;
            }

            if (_inputHandler == null || _playerController == null)
                return;

            // 敵参照がない場合に自動検索
            if (_enemyTransform == null && _autoSearchEnemyByTag)
            {
                GameObject enemyObj = GameObject.FindWithTag("Enemy");
                if (enemyObj != null)
                {
                    _enemyTransform = enemyObj.transform;
                }
            }

            // 敵が存在しない、または非アクティブな場合は逃避判定を行わない（タイマー減少）
            if (
                _enemyTransform == null
                || (_requireActiveEnemy && !_enemyTransform.gameObject.activeInHierarchy)
            )
            {
                _flightTimer = Mathf.Max(0f, _flightTimer - Time.deltaTime);
                return;
            }

            bool isMoving = _inputHandler.move.sqrMagnitude > 0.01f;
            if (!isMoving)
            {
                _flightTimer = 0f;
                return;
            }

            // カメラの向き（XZ平面）と敵方向の角度差を計算
            Vector3 camForwardXZ =
                Camera.main != null
                    ? new Vector3(
                        Camera.main.transform.forward.x,
                        0f,
                        Camera.main.transform.forward.z
                    ).normalized
                    : Vector3.forward;
            Vector3 toEnemyXZ = new Vector3(
                _enemyTransform.position.x - _playerController.transform.position.x,
                0f,
                _enemyTransform.position.z - _playerController.transform.position.z
            ).normalized;

            float angle = Vector3.Angle(camForwardXZ, toEnemyXZ);
            bool lookingAwayFromEnemy = angle >= _flightAwayAngleThreshold;

            if (lookingAwayFromEnemy)
            {
                _flightTimer += Time.deltaTime;
                if (_flightTimer >= 1f)
                {
                    NotifySignal("逃避");
                }
            }
            else
            {
                _flightTimer = Mathf.Max(0f, _flightTimer - Time.deltaTime);
            }
        }

        /// <summary>
        /// 指標2: スパム
        /// スライディングウィンドウ内での防御ボタンの押下回数から頻度（回/秒）を算出する。
        /// ボタンの実際の押下はイベント（OnDefenseButtonPressed）で受け取り、ここでは頻度を計算する。
        /// </summary>
        private void DetectSpam()
        {
            _spamElapsed += Time.deltaTime;

            // ウィンドウ幅を超えたらリセット
            if (_spamElapsed >= _spamWindowDuration)
            {
                // カウントが1回の時は、プレイヤーが冷静に防御しているあるいはパリィしている可能性があるので含めない。
                if (_spamPressCount > 1)
                {
                    SpamRate = _spamPressCount / _spamElapsed;
                    SpamScore = Mathf.Clamp01(SpamRate / _spamMaxRate);
                }

                if (SpamRate >= 1f)
                {
                    NotifySignal("スパム");
                }

                _spamPressCount = 0;
                _spamElapsed = 0f;
            }
        }

        /// <summary>
        /// 指標3: ジッター
        /// 弓を構えている間のカメラのヨー角速度（°/秒）を計算し、急激な動きを検出する。
        /// </summary>
        private void DetectJitter()
        {
            if (_bowController == null || _playerController == null)
            {
                JitterScore = 0f;
                return;
            }

            bool isAiming = _bowController.IsAiming;
            if (!isAiming)
            {
                JitterScore = 0f;
                _jitterAngularSpeed = 0f;
                // 非構え中は前フレームのヨーを更新しておく（構えに入った直後の誤検知を防ぐ）
                if (_playerController.CinemachineCameraTarget != null)
                    _prevCamYaw = _playerController.CinemachineCameraTarget.transform.eulerAngles.y;
                return;
            }

            if (_playerController.CinemachineCameraTarget == null)
                return;

            float currentYaw = _playerController.CinemachineCameraTarget.transform.eulerAngles.y;

            // 角度のラッピング（-180〜180に正規化）して急反転を正確に計測する
            float delta = Mathf.DeltaAngle(_prevCamYaw, currentYaw);
            _jitterAngularSpeed = Mathf.Abs(delta) / Mathf.Max(Time.deltaTime, 0.001f);
            _prevCamYaw = currentYaw;

            JitterScore = Mathf.Clamp01(_jitterAngularSpeed / _jitterMaxAngularSpeed);

            if (_jitterAngularSpeed >= _jitterMaxAngularSpeed * 0.5f)
            {
                NotifySignal("ジッター");
            }
        }

        /// <summary>
        /// 指標4: 空間喪失
        /// 移動入力があるにもかかわらず、実際の速度が閾値以下の状態の継続時間を計測する。
        /// 壁や地形に引っかかって動けない状態を検出する。
        /// </summary>
        private void DetectTrap()
        {
            if (_inputHandler == null || _characterController == null || _playerController == null)
                return;

            if (!_playerController.CanMove)
                return;

            bool hasMoveinput = _inputHandler.move.sqrMagnitude > 0.01f;
            float horizontalSpeed = new Vector3(
                _characterController.velocity.x,
                0f,
                _characterController.velocity.z
            ).magnitude;
            bool isStuck = horizontalSpeed < _trapVelocityThreshold;

            if (hasMoveinput && isStuck)
            {
                _trapTimer += Time.deltaTime;
                if (_trapTimer >= 0.5f)
                {
                    NotifySignal("空間喪失");
                }
            }
            else
            {
                _trapTimer = Mathf.Max(0f, _trapTimer - Time.deltaTime * 2f);
            }
        }

        /// <summary>
        /// 総合焦り度（0〜100）を算出する。
        ///
        /// 【設計方針】
        /// 「1つの行動を強く繰り返す」こと自体も焦りの強いシグナルであるため、
        ///  単純な重み付き合計ではなく「最大値主体 + 多様性ボーナス」の式を採用する。
        ///
        ///  PanicScore = max(各指標スコア) × 70  ← 1行動の強度を主軸にする
        ///             + avg(全指標スコア) × 30  ← 複数行動の組み合わせでさらに上昇
        ///
        /// 結果:
        ///   1指標だけ 100%  → 約 75〜78 （明確な焦り状態と判定）
        ///   全指標が  100%  → 100        （極度の焦り）
        ///
        /// 毎フレーム自然減衰も適用する。
        /// </summary>
        private void CalculatePanicScore()
        {
            float f = FlightScore;
            float s = SpamScore;
            float j = JitterScore;
            float t = TrapScore;
            float a = AimCancelScore;
            float h = HitChainScore;

            // 最大値主体スコア（1つの行動が強ければ高く出る）
            // PanicScore = max(各指標) × 70 + (∑全指標) / 6 × 30
            float maxScore = Mathf.Max(f, s, j, t, a, h);

            // 多様性ボーナス（複数指標が同時に高いほど上乗せ）
            float avgScore = (f + s + j + t + a + h) / 6f;

            float raw = maxScore * 70f + avgScore * 30f;

            // 上昇は素早く追いつき、自然減衰はゆっくり（Inspector設定）
            if (raw > PanicScore)
            {
                PanicScore = Mathf.Lerp(PanicScore, raw, Time.deltaTime * 5f);
            }
            else
            {
                PanicScore = Mathf.Max(0f, PanicScore - _panicDecayPerSecond * Time.deltaTime);
            }

            PanicScore = Mathf.Clamp(PanicScore, 0f, 100f);
        }

        /// <summary>
        /// 指標5: 意思崩壊
        /// 弓を構えていた（IsAiming: true）状態から構えが解除された（IsAiming: false）タイミングで、
        /// 直前に発射が成功していない場合のみキャンセルとしてカウントする。
        ///
        /// 発射成功時も StateFire → StateRelease の遷移で IsAiming が false・CanFire が false になるため、
        /// CanFire だけでは発射とキャンセルを区別できない。
        /// BowController.OnFired（発射成功イベント）を購読して _justFired フラグで区別する。
        /// </summary>
        private void DetectAimCancel()
        {
            if (_bowController == null)
                return;

            bool isAimingNow = _bowController.IsAiming;

            if (_wasAiming && !isAimingNow)
            {
                if (_justFired)
                {
                    // 正常発射によるエイム解除 → キャンセルとしてカウントしない
                    _justFired = false;
                }
                else
                {
                    // 撃たずに構えを解除 → 意思崩壊
                    _aimCancelCount++;
                    NotifySignal("意思崩壊");
                }
            }

            _wasAiming = isAimingNow;
        }

        /// <summary>
        /// 矢が正常に発射されたときに BowController.OnFired から呼ばれる。
        /// 次フレームの DetectAimCancel での誤カウントを防ぐため、フラグを立てる。
        /// </summary>
        private void OnArrowFired()
        {
            _justFired = true;
        }

        /// <summary>
        /// 防御ボタンが新たに押されたときに PlayerInputHandler から呼ばれる。
        /// 剣を持っているときのみスパムとしてカウントする。
        /// </summary>
        private void OnDefenseButtonPressed()
        {
            // WeaponManager で剣が選択されているときのみカウント
            // 弓構え中（IsAiming）はスパムとしてカウントしない
            bool bowIsAiming = _bowController != null && _bowController.IsAiming;
            if (bowIsAiming)
                return;

            _spamPressCount++;
        }

        /// <summary>
        /// プレイヤーのHPが変化したときに PlayerStatus から呼ばれる。
        /// 連続被弾（回避失敗）の検出に使用する。
        /// </summary>
        private void OnPlayerHpChanged(float currentHp, float maxHp)
        {
            // シーン開始直後の初期化イベントは記録のみで処理終了
            if (_lastHp < 0f)
            {
                _lastHp = currentHp;
                return;
            }

            // 回復やHP不変の場合は記録のみで処理終了
            if (currentHp >= _lastHp)
            {
                _lastHp = currentHp;
                return;
            }

            // ダメージ受けてHPが減少した場合
            _lastHp = currentHp;
            float now = Time.time;

            // 前回の被弾から一定時間以内なら連続被弾としてカウント
            if (now - _lastHitTime <= _hitChainWindow)
            {
                _hitChainCount++;
                NotifySignal("回避失敗");
            }
            else
            {
                // 単発被弾（連続被弾チェーンの1回目スタート）
                _hitChainCount = 1;
            }

            _lastHitTime = now;
        }

        /// <summary>
        /// シグナルを記録し、重複通知を避けながらイベントを発火する。
        /// </summary>
        private void NotifySignal(string signalName)
        {
            LastDetectedSignal = signalName;
            OnPanicSignalDetected?.Invoke(signalName);
        }

        /// <summary>
        /// 敵が死亡したり戦闘が終了したときなど、外部からリセットを呼べる公開メソッド。
        /// </summary>
        public void ResetAllScores()
        {
            _flightTimer = 0f;
            _spamPressCount = 0;
            _spamElapsed = 0f;
            SpamScore = 0f;
            SpamRate = 0f;
            JitterScore = 0f;
            _jitterAngularSpeed = 0f;
            _trapTimer = 0f;
            _aimCancelCount = 0;
            _hitChainCount = 0;
            _lastHitTime = -999f;
            _lastHp = -1f;
            _justFired = false;
            PanicScore = 0f;
            LastDetectedSignal = "なし";
        }
    }
}
