using System;
using SoundKit;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓の状態管理・弦表現・矢の生成/射出を統括するコンポーネント。ステートは BowStates.cs。
    ///
    /// タイミングはアニメーションイベントで決まり、スクリプトは秒数を持たない:
    /// - 構えクリップ(UpperBody の Aim ステート)の DrawComplete : 引き切り。矢をつがえて発射可能にする
    /// - 発射クリップ(UpperBody の Fire ステート)の ReleaseEnd   : 発射の戻りが終わり、次の矢を引ける
    /// 弦の中点は手元の矢に追従する(アニメーションで手が動けば弦も引かれる)。
    /// 照準の表示はシーン側の CrosshairView が PlayerController.IsAiming を見て行う。
    /// </summary>
    public class BowController : MonoBehaviour, IResettableWeapon, IWeaponAnimationEventHandler
    {
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

        [Tooltip("弦を引いていない時の中点（任意。空欄なら上端と下端の中点）")]
        [SerializeField]
        private Transform _stringRestMidPoint;

        [Header("矢の設定")]
        [Tooltip("矢の飛翔速度 (m/s)。矢の Prefab はシーンの ArrowPool に設定する")]
        [SerializeField]
        private float _arrowSpeed = 50f;

        [Tooltip("クロスヘアーRaycastの最大距離 (m)")]
        [SerializeField]
        private float _maxRange = 200f;

        [Header("SE設定")]
        [Tooltip("弦を引く音。引き切って矢をつがえた瞬間(DrawComplete)に鳴らす")]
        [SerializeField]
        private SoundCue _drawSound;

        [Tooltip("矢が発射される音")]
        [SerializeField]
        private SoundCue _shootSound;

        private WeaponManager _weaponManager;
        private PlayerStatus _owner;
        private Transform _arrowHandHoldPoint;
        private MockWeaponPose _mockPose;
        private Camera _mainCamera;
        private BowState _currentState;
        private GameObject _nockedArrow;
        private int _upperBodyLayer;

        // --- ステート(BowStates.cs)から使う参照と状態 ---
        public PlayerInputHandler Input { get; private set; }
        public PlayerController Player { get; private set; }
        public Animator Animator { get; private set; }

        /// <summary>矢をつがえ終わり、発射できる状態か。</summary>
        public bool IsArrowNocked { get; private set; }

        /// <summary>構えている(引いている)最中か。</summary>
        public bool IsAiming => _currentState is BowStateAim;

        /// <summary>
        /// 矢が発射された瞬間に発火する静的イベント。
        /// BowZoomController などが購読して発射演出を行う。
        /// </summary>
        public static event Action OnFired;

        /// <summary>
        /// プレイヤーが自分でボタンを離し、撃たずに構えを解いた瞬間に発火する。
        /// 怯み・掴み・武器切替などによる強制解除では発火しない(PanicDetector が「迷い」として数えるため区別する)。
        /// </summary>
        public event Action OnAimCancelled;

        /// <summary>構えステートから、プレイヤー自身による構え解除を通知する。</summary>
        public void NotifyAimCancelled() => OnAimCancelled?.Invoke();

        private void Awake()
        {
            Input = GetComponentInParent<PlayerInputHandler>();
            Player = GetComponentInParent<PlayerController>();
            _weaponManager = GetComponentInParent<WeaponManager>();
            _owner = GetComponentInParent<PlayerStatus>();

            PlayerModel model = PlayerModel.Of(this);
            Animator = model.Animator;
            _upperBodyLayer = Animator.GetLayerIndex(PlayerAnimatorStates.UpperBodyLayerName);
            // 矢が最初に現れる位置(引き手)はモデルごとに違うので、モデル側のソケットから取る
            _arrowHandHoldPoint = model.GetSocket(PlayerSocketId.ArrowHold);

            // 仮モデル専用の見た目補正。本番アニメーションで向きが付いたら外す(無ければ何もしない)
            TryGetComponent(out _mockPose);

            Assert.IsTrue(
                _upperBodyLayer >= 0,
                $"{name}: Animator に「{PlayerAnimatorStates.UpperBodyLayerName}」レイヤーがありません"
            );
            Assert.IsNotNull(_owner, $"{name}: 親階層に PlayerStatus が見つかりません");
            Assert.IsNotNull(_arrowNockPoint, $"{name}: _arrowNockPoint が未設定です");
            Assert.IsNotNull(_bowStringRenderer, $"{name}: _bowStringRenderer が未設定です");
            Assert.IsNotNull(_stringTopPoint, $"{name}: _stringTopPoint が未設定です");
            Assert.IsNotNull(_stringBottomPoint, $"{name}: _stringBottomPoint が未設定です");
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            Assert.IsNotNull(_mainCamera, $"{name}: MainCamera タグのカメラが見つかりません");
            Assert.IsNotNull(ArrowPool.Instance, $"{name}: シーンに ArrowPool がありません");
            _bowStringRenderer.positionCount = 3;
            _bowStringRenderer.useWorldSpace = true;
            UpdateBowString();
        }

        private void OnEnable()
        {
            ChangeState(new BowStateFree(this));
        }

        private void OnDisable()
        {
            // 武器をしまったら現在の行動を強制終了し、プレイヤーのロックを解除する
            _currentState.Exit();
            Player.IsAiming = false;
            Player.Locks.Release(this);

            // 弓のトリガーが残ったまま別武器に切り替わると誤発動するためリセット
            Animator.ResetTrigger(PlayerAnimatorParams.Fire);

            DestroyArrow();
            UpdateBowString();
        }

        private void Update()
        {
            bool canAct =
                !Player.IsFlinching
                && !Player.IsGrabbed
                // 番号ではなく「選択中の武器オブジェクトが自分か」で判定する(Inspector で番号を合わせる必要はない)
                && _weaponManager.CurrentWeaponObject == gameObject;

            if (!canAct)
            {
                if (_currentState is not BowStateFree)
                    ChangeState(new BowStateFree(this));
            }
            else
            {
                _currentState.Update();
            }

            UpdateBowString();

            if (_mockPose != null)
                _mockPose.SetPose(Player.IsAiming ? MockWeaponPoseKey.Aim : MockWeaponPoseKey.Idle);
        }

        public void ChangeState(BowState newState)
        {
            // 初回(OnEnable)は前のステートが無い
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        public void OnWeaponAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            _currentState.OnAnimationEvent(type, sourceState);
        }

        /// <summary>UpperBody レイヤーで、指定のステートを再生中か、そこへ遷移中か。</summary>
        public bool IsInUpperBodyState(int stateHash) =>
            Animator.GetCurrentAnimatorStateInfo(_upperBodyLayer).shortNameHash == stateHash
            || (
                Animator.IsInTransition(_upperBodyLayer)
                && Animator.GetNextAnimatorStateInfo(_upperBodyLayer).shortNameHash == stateHash
            );

        /// <summary>UpperBody レイヤーで現在再生中のステートの進み具合(1 で1周)。</summary>
        public float UpperBodyNormalizedTime =>
            Animator.GetCurrentAnimatorStateInfo(_upperBodyLayer).normalizedTime;

        public void SpawnArrowInHand()
        {
            if (_nockedArrow != null)
                return;

            ArrowProjectile arrow = ArrowPool.Instance.Get();
            arrow.transform.SetParent(_arrowHandHoldPoint);
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            _nockedArrow = arrow.gameObject;
        }

        /// <summary>手元の矢を弓につがえて発射可能にする(DrawComplete イベント)。</summary>
        public void NockArrow()
        {
            if (_nockedArrow == null || IsArrowNocked)
                return;

            _nockedArrow.transform.SetParent(_arrowNockPoint, false);
            _nockedArrow.transform.localPosition = Vector3.zero;
            _nockedArrow.transform.localRotation = Quaternion.identity;
            IsArrowNocked = true;
            WorldSoundPlayer.Play(_drawSound, transform.position, this);
        }

        public void FireArrow()
        {
            if (_nockedArrow == null)
                return;

            Vector3 direction = GetShootDirection();
            _nockedArrow.transform.SetParent(null);

            _nockedArrow.GetComponent<ArrowProjectile>().Launch(direction, _arrowSpeed, _owner);

            _nockedArrow = null;
            IsArrowNocked = false;

            OnFired?.Invoke();

            Animator.SetTrigger(PlayerAnimatorParams.Fire);
            WorldSoundPlayer.Play(_shootSound, transform.position, this);
        }

        public void DestroyArrow()
        {
            if (_nockedArrow != null)
            {
                _nockedArrow.GetComponent<ArrowProjectile>().ReturnToPool();
                _nockedArrow = null;
            }
            IsArrowNocked = false;
        }

        /// <summary>
        /// 画面中央（クロスヘアー位置）からRaycastして実際のワールド上の着弾点を求め、
        /// 矢のNockPointからその着弾点への方向ベクトルを返す。
        /// これにより三人称視点でもカメラが向いている方向に矢が正確に飛ぶ。
        /// </summary>
        private Vector3 GetShootDirection()
        {
            Ray ray = _mainCamera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, _maxRange)
                ? hit.point
                : ray.GetPoint(_maxRange);

            Vector3 dir = targetPoint - _arrowNockPoint.position;
            // 着弾点と矢の位置がほぼ一致する場合（ゼロ除算防止）はカメラの方向をフォールバックとする
            return dir.sqrMagnitude < 0.0001f ? ray.direction : dir.normalized;
        }

        /// <summary>
        /// 弦の LineRenderer を更新する。3点（上端・中点・下端）で弦を表し、
        /// 矢を持っている間は中点を矢の後端(手元またはノック位置)に合わせる。
        /// 引く速さは構えアニメーションの手の動きで決まり、秒数の設定は持たない。
        /// </summary>
        private void UpdateBowString()
        {
            Vector3 midPoint;
            if (_nockedArrow != null)
                midPoint = _nockedArrow.transform.position;
            else if (_stringRestMidPoint != null)
                midPoint = _stringRestMidPoint.position;
            else
                midPoint = Vector3.Lerp(
                    _stringTopPoint.position,
                    _stringBottomPoint.position,
                    0.5f
                );

            _bowStringRenderer.SetPosition(0, _stringTopPoint.position);
            _bowStringRenderer.SetPosition(1, midPoint);
            _bowStringRenderer.SetPosition(2, _stringBottomPoint.position);
        }

        /// <summary>
        /// 怯み・死亡時(WeaponManager.ResetEquippedWeapon 経由)に、弓のステートを待機へ戻す。
        /// 行動制限は owner ごとに管理されるので、待機の Enter で弓の分を外しても怯み側の制限は残る。
        /// </summary>
        public void ForceReset()
        {
            ChangeState(new BowStateFree(this));
            UpdateBowString();
        }
    }
}
