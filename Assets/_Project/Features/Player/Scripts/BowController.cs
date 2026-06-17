using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓矢システムの統括コンポーネント。
    /// このスクリプトは弓オブジェクト本体にアタッチして使用します。
    /// PlayerInputHandler と Animator は親のプレイヤーオブジェクトから自動取得します。
    ///
    /// ■ 状態の流れ
    ///   Aim開始
    ///     → 矢が _arrowHandHoldPoint（手）に生成
    ///     → _drawProgress が 0 → 1 へ増加（弦が滑らかに引かれる）
    ///     → 引き切り完了（drawProgress = 1）時に矢を _arrowNockPoint へ移動
    ///     → 発射可能になる
    ///   発射
    ///     → 矢が飛翔
    ///     → _drawProgress が 1 → 0 へ減少（弦が滑らかに戻る）
    ///     → 戻り完了後、まだ Aim 中なら次の矢を装填して再び引き始める
    ///   Aim終了
    ///     → 矢を消去
    ///     → _drawProgress が 0 へ戻る
    ///
    /// ■ セットアップ手順（1回だけ）
    ///   1. PlayerArmature の右手ボーンの子に「BowMountPoint」（空GO）を追加
    ///   2. BowMountPoint の子に弓Prefabを配置し、ローカル位置・回転を調整
    ///   3. 弓Prefab内に以下の空GOを配置：
    ///      - ArrowNockPoint    : 矢がセットされる点（引き切ったときの矢の位置）
    ///      - StringTopPoint    : 弦の上端
    ///      - StringBottomPoint : 弦の下端
    ///      - StringRestMid     : 弦を引いていないときの中点
    ///   4. _arrowHandHoldPoint には「引く手（右手）のボーンまたは空GO」を設定
    ///   5. _stringPullTarget  には「引く手のボーン」を設定（弦の引き手位置）
    ///   6. Inspector から各Transform・LineRendererを参照設定
    ///   ※ _bowRootTransform を未設定にすると、弓オブジェクト自身（this.transform）が使われます
    ///
    /// ■ 矢Prefabの構造
    ///   ArrowNockRoot（空GO・Pivotがノック端）← ArrowProjectile + CapsuleCollider（初期無効）
    ///     └── ArrowMesh（ノックが原点に来るようオフセット配置）
    /// </summary>
    public class BowController : MonoBehaviour
    {
        [Header("武器管理")]
        [Tooltip("WeaponManagerで弓が割り当てられているインデックス (通常は 1)")]
        public int weaponIndex = 1;

        [Header("弓の参照（弓Prefab内の各Transform）")]
        [Tooltip("矢がセットされる点（弓Prefab内の空GO・引き切ったときの矢の位置）")]
        [SerializeField]
        private Transform _arrowNockPoint;

        [Tooltip("弓の弦（弓Prefab内のLineRenderer）")]
        [SerializeField]
        private LineRenderer _bowStringRenderer;

        [Tooltip("弦の上端（弓Prefab内の空GO）")]
        [SerializeField]
        private Transform _stringTopPoint;

        [Tooltip("弦の下端（弓Prefab内の空GO）")]
        [SerializeField]
        private Transform _stringBottomPoint;

        [Tooltip("弦を引いていない時の中点（弓Prefab内の空GO）")]
        [SerializeField]
        private Transform _stringRestMidPoint;

        [Tooltip("弦を引く手のTransform（右手ボーンなど）。引いているとき弦の中点がここに寄る")]
        [SerializeField]
        private Transform _stringPullTarget;

        [Header("矢の設定")]
        [Tooltip("矢のPrefab（ArrowProjectileコンポーネント必須・PivotはNock端）")]
        [SerializeField]
        private GameObject _arrowPrefab;

        [Tooltip("矢がAim開始時に最初に現れる位置（引く手のボーンや空GO）")]
        [SerializeField]
        private Transform _arrowHandHoldPoint;

        [Tooltip("矢の飛翔速度 (m/s)")]
        [SerializeField]
        private float _arrowSpeed = 50f;

        [Tooltip("クロスヘアーRaycastの最大距離 (m)")]
        [SerializeField]
        private float _maxRange = 200f;

        [Header("引き・戻し")]
        [Tooltip("弦を引ききるまでの時間（秒）。引き動作アニメーションに合わせて調整する")]
        [SerializeField]
        public float _drawDuration = 0.5f;

        [Tooltip("発射後に弦が元に戻るまでの時間（秒）")]
        [SerializeField]
        public float _releaseDuration = 0.15f;

        [Header("アニメーション")]
        [Tooltip("発射アニメーションのTriggerパラメータ名")]
        [SerializeField]
        private string _fireTriggerName = "Fire";

        [Header("AimHUD")]
        [Tooltip("Aim時に表示されるクロスヘアー")]
        [SerializeField]
        private Image crossHairImage;

        [Header("弓の構え角度補正")]
        [Tooltip("回転させる弓のルートオブジェクト（手の子になっている弓本体）")]
        [SerializeField]
        private Transform _bowRootTransform;

        [Header("弓の構え角度補正")]
        [Tooltip("エイム中（構え時）の弓のローカル角度。今の完璧な角度を入れます")]
        [SerializeField]
        private Vector3 _aimLocalRotation = Vector3.zero;

        [Tooltip(
            "待機中（走っている時など）の弓のローカル角度。串刺しにならない自然な角度を探して入れます"
        )]
        [SerializeField]
        private Vector3 _idleLocalRotation = new Vector3(90f, 0f, 0f);

        [Tooltip("角度が切り替わるスピード")]
        [SerializeField]
        private float _rotationSpeed = 15f;

        // -------------------------------------------------------
        // 内部状態（BowStates.cs からアクセスするため public）
        // -------------------------------------------------------

        [HideInInspector]
        public PlayerInputHandler _input;

        [HideInInspector]
        public PlayerController _playerController;

        [HideInInspector]
        public Animator _animator;

        [HideInInspector]
        public float _drawProgress = 0f;

        [HideInInspector]
        public bool _isArrowAtNock = false;

        private WeaponManager _weaponManager;
        private Camera _mainCamera;
        private BowState _currentState;
        private GameObject _nockedArrow;

        public float DrawProgress => _drawProgress;
        public bool IsAiming => _currentState is StateAim;
        public bool CanFire => _isArrowAtNock;

        /// <summary>
        /// 矢が発射された瞬間に発火する静的イベント。
        /// BowZoomController などが購読して発射演出を行う。
        /// </summary>
        public static event Action OnFired;

        // -------------------------------------------------------

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputHandler>();
            _animator = GetComponentInParent<Animator>();
            _playerController = GetComponentInParent<PlayerController>();
            _weaponManager = GetComponentInParent<WeaponManager>();

            if (_bowRootTransform == null)
                _bowRootTransform = transform;
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            InitBowString();
            HideCrossHair();
        }

        private void OnEnable()
        {
            // 武器を持った瞬間はFree（待機）ステートから開始
            ChangeState(new StateFree(this));
        }

        private void OnDisable()
        {
            // 武器をしまったら現在の行動を強制終了し、プレイヤーのロックを解除する
            _currentState?.Exit();
            if (_playerController != null)
            {
                _playerController.IsAiming = false;
                _playerController.CanChangeWeapon = true;
            }

            // 弓のトリガーが残らないようにリセット
            if (_animator != null && !string.IsNullOrEmpty(_fireTriggerName))
            {
                _animator.ResetTrigger(_fireTriggerName);
            }

            DestroyArrow();
            _drawProgress = 0f;
            UpdateBowString(0f);
        }

        private void Update()
        {
            if (_input == null || _playerController == null)
                return;

            // 怯み中は弓のステートマシンを完全停止する
            if (_playerController.IsFlinching)
                return;

            // WeaponManagerが存在する場合のみ、自分のターンか確認する
            if (_weaponManager != null && _weaponManager.CurrentWeaponIndex != weaponIndex)
            {
                if (!(_currentState is StateFree))
                    ChangeState(new StateFree(this));
                UpdateBowString(0f);
                return;
            }

            _currentState?.Update();
            UpdateBowString(_drawProgress);

            if (_bowRootTransform != null)
            {
                Vector3 targetEuler = _playerController.IsAiming
                    ? _aimLocalRotation
                    : _idleLocalRotation;
                _bowRootTransform.localRotation = Quaternion.Slerp(
                    _bowRootTransform.localRotation,
                    Quaternion.Euler(targetEuler),
                    Time.deltaTime * _rotationSpeed
                );
            }
        }

        public void ChangeState(BowState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        // ==========================================================
        // 以降は弓の具体的なアクションメソッド（ステートから呼ばれる）
        // ==========================================================

        public void SpawnArrowInHand()
        {
            if (_nockedArrow != null)
                return;
            if (_arrowPrefab == null)
                return;

            Transform spawnParent =
                _arrowHandHoldPoint != null ? _arrowHandHoldPoint : _arrowNockPoint;
            if (spawnParent == null)
                return;

            _nockedArrow = Instantiate(_arrowPrefab, spawnParent);
            _nockedArrow.transform.localPosition = Vector3.zero;
            _nockedArrow.transform.localRotation = Quaternion.identity;
        }

        public void MoveArrowToNock()
        {
            if (_nockedArrow != null && _arrowNockPoint != null)
            {
                _nockedArrow.transform.SetParent(_arrowNockPoint, false);
                _nockedArrow.transform.localPosition = Vector3.zero;
                _nockedArrow.transform.localRotation = Quaternion.identity;
                _isArrowAtNock = true;
            }
        }

        public void FireArrow()
        {
            if (_nockedArrow == null)
                return;

            Vector3 direction = GetShootDirection();
            _nockedArrow.transform.SetParent(null);

            ArrowProjectile proj = _nockedArrow.GetComponent<ArrowProjectile>();
            if (proj != null)
            {
                proj.Launch(direction, _arrowSpeed);
            }
            else
            {
                Rigidbody rb = _nockedArrow.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.linearVelocity = direction * _arrowSpeed;
                _nockedArrow.transform.rotation = Quaternion.LookRotation(direction);
                Destroy(_nockedArrow, 5f);
            }

            _nockedArrow = null;
            _isArrowAtNock = false;

            // 発射イベントを通知（BowZoomController などが受け取る）
            OnFired?.Invoke();

            if (_animator != null && !string.IsNullOrEmpty(_fireTriggerName))
                _animator.SetTrigger(_fireTriggerName);
        }

        public void DestroyArrow()
        {
            if (_nockedArrow != null)
            {
                Destroy(_nockedArrow);
                _nockedArrow = null;
            }
            _isArrowAtNock = false;
        }

        private Vector3 GetShootDirection()
        {
            if (_mainCamera == null)
                return transform.parent != null ? transform.parent.forward : transform.forward;

            Ray ray = _mainCamera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, _maxRange)
                ? hit.point
                : ray.GetPoint(_maxRange);

            if (_arrowNockPoint != null)
            {
                Vector3 dir = targetPoint - _arrowNockPoint.position;
                return dir.sqrMagnitude < 0.0001f ? ray.direction : dir.normalized;
            }
            return ray.direction;
        }

        private void InitBowString()
        {
            if (_bowStringRenderer == null)
                return;
            _bowStringRenderer.positionCount = 3;
            _bowStringRenderer.useWorldSpace = true;
            UpdateBowString(0f);
        }

        private void UpdateBowString(float progress)
        {
            if (_bowStringRenderer == null || _stringTopPoint == null || _stringBottomPoint == null)
                return;

            Vector3 restMid =
                _stringRestMidPoint != null
                    ? _stringRestMidPoint.position
                    : Vector3.Lerp(_stringTopPoint.position, _stringBottomPoint.position, 0.5f);
            Vector3 pulledMid = _stringPullTarget != null ? _stringPullTarget.position : restMid;
            Vector3 midPoint = Vector3.Lerp(restMid, pulledMid, progress);

            _bowStringRenderer.SetPosition(0, _stringTopPoint.position);
            _bowStringRenderer.SetPosition(1, midPoint);
            _bowStringRenderer.SetPosition(2, _stringBottomPoint.position);
        }

        public void ShowCrossHair()
        {
            if (crossHairImage != null)
            {
                crossHairImage.enabled = true;
            }
        }

        public void HideCrossHair()
        {
            if (crossHairImage != null)
            {
                crossHairImage.enabled = false;
            }
        }

        /// <summary>
        /// PlayerFlinchHandler から呼ばれる。弓のステートを安全にリセットする。
        /// StateFree.Enter() を呼び、矢の破棄・ IsAiming = false ・クロスヘア非表示を確実に実行する。
        /// 呼び出し後、FlinchHandler が CanChangeWeapon = false に上書きするので
        /// ここでは CanChangeWeapon には触れない。
        /// </summary>
        public void ForceReset()
        {
            _currentState?.Exit();
            // StateFree に直接移行し Enter() を呼んでクリーンアップを完了させる
            // StateFree.Enter() 内容： IsAiming=false, CanChangeWeapon=true, DrawProgress=0, DestroyArrow, HideCrossHair
            _currentState = new StateFree(this);
            _currentState.Enter();
            // CanChangeWeapon は FlinchHandler 側が強制的に false に上書きするのでここではそのまま
            _drawProgress = 0f;
            UpdateBowString(0f);
        }
    }
}
