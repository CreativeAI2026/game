using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓の状態管理・弦表現・矢の生成/射出を統括するコンポーネント。
    /// ステートマシン（BowStates.cs）と密接に連携し、各ステートから
    /// 内部フィールドへ直接アクセスするため、フィールドをpublicにしている。
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

        [Header("SE設定")]
        [Tooltip("弦を引く音")]
        [SerializeField]
        private AudioClip _drawSound;
        public AudioClip DrawSound => _drawSound;

        [Tooltip("矢が発射される音")]
        [SerializeField]
        private AudioClip _shootSound;
        public AudioClip ShootSound => _shootSound;

        private AudioSource _audioSource;
        public AudioSource ASource => _audioSource;

        // BowStates.cs の各ステートクラスから直接アクセスするため、publicにしている
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

        private void Awake()
        {
            _input = GetComponentInParent<PlayerInputHandler>();
            _animator = GetComponentInParent<Animator>();
            _playerController = GetComponentInParent<PlayerController>();
            _weaponManager = GetComponentInParent<WeaponManager>();
            _audioSource = GetComponent<AudioSource>();

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

            // 弓のトリガーが残ったまま別武器に切り替わると誤発動するためリセット
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

            if (_playerController.IsFlinching)
                return;

            if (_weaponManager != null && _weaponManager.CurrentWeaponIndex != weaponIndex)
            {
                if (_currentState is not StateFree)
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

            // ArrowPoolが存在すればプールから取得し、なければInstantiateにフォールバック
            if (ArrowPool.Instance != null)
            {
                ArrowProjectile pooledArrow = ArrowPool.Instance.Get();
                pooledArrow.transform.SetParent(spawnParent);
                pooledArrow.transform.localPosition = Vector3.zero;
                pooledArrow.transform.localRotation = Quaternion.identity;
                _nockedArrow = pooledArrow.gameObject;
            }
            else
            {
                _nockedArrow = Instantiate(_arrowPrefab, spawnParent);
                _nockedArrow.transform.localPosition = Vector3.zero;
                _nockedArrow.transform.localRotation = Quaternion.identity;
            }
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

            OnFired?.Invoke();

            if (_animator != null && !string.IsNullOrEmpty(_fireTriggerName))
                _animator.SetTrigger(_fireTriggerName);
        }

        public void DestroyArrow()
        {
            if (_nockedArrow != null)
            {
                // ArrowProjectileがあればプールへ返却、なければDestroyにフォールバック
                ArrowProjectile proj = _nockedArrow.GetComponent<ArrowProjectile>();
                if (proj != null)
                {
                    proj.ReturnToPool();
                }
                else
                {
                    Destroy(_nockedArrow);
                }
                _nockedArrow = null;
            }
            _isArrowAtNock = false;
        }

        /// <summary>
        /// 画面中央（クロスヘアー位置）からRaycastして実際のワールド上の着弾点を求め、
        /// 矢のNockPointからその着弾点への方向ベクトルを返す。
        /// これにより三人称視点でもカメラが向いている方向に矢が正確に飛ぶ。
        /// </summary>
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
                // 着弾点と矢の位置がほぼ一致する場合（ゼロ除算防止）はカメラの方向をフォールバックとする
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

        /// <summary>
        /// 弦のLineRendererを弓の引き度合い（0〜1）に応じて更新する。
        /// 3点（上端・中点・下端）で弦を表現し、中点を引く手の位置に補間で寄せることで
        /// 弓を引くビジュアルを実現している。
        /// </summary>
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
        /// PlayerFlinchHandler から呼ばれ、弓のステートを安全にリセットする。
        /// 呼び出し後に FlinchHandler が CanChangeWeapon = false に上書きするため、
        /// ここでは CanChangeWeapon には触れない。
        /// </summary>
        public void ForceReset()
        {
            _currentState?.Exit();
            _currentState = new StateFree(this);
            _currentState.Enter();

            _drawProgress = 0f;
            UpdateBowString(0f);
        }
    }
}
