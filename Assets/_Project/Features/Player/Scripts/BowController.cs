using System;
using SoundKit;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓の状態管理・弦表現・矢の生成/射出を統括するコンポーネント。ステートは BowStates.cs。
    ///
    /// タイミングはアニメーションイベントで決まり、スクリプトは秒数を持たない:
    /// - 構えクリップ(UpperBody の Aim ステート)の GenerateArrow : 引き手に矢を出す(背中から矢を取る動きに合わせる)
    /// - 構えクリップ(UpperBody の Aim ステート)の DrawComplete : 矢を弦につがえる。以後、弦は引き手に付いて引かれ、発射可能になる
    /// - 発射クリップ(UpperBody の Fire ステート)の ReleaseEnd   : 発射の戻りが終わり、次の矢を引ける
    ///
    /// 弦: モデルの弦は根本ボーンに付いている。つがえてから発射までは、根本ボーンを引き手(ArrowHold ソケット)に合わせて動かし、
    /// 発射したら元の位置へ戻す(<see cref="BowStringPull"/>)。つがえた矢は尾を弦に掛け、弓の矢の通り道へ向ける。
    /// 照準の表示はシーン側の CrosshairView が PlayerController.IsAiming を見て行う。
    /// </summary>
    public class BowController : MonoBehaviour, IResettableWeapon, IWeaponAnimationEventHandler
    {
        [Header("弓の参照（弓Prefab内の各Transform）")]
        [Tooltip("弦の引き点になるモデルの骨（根本）。弦のメッシュはこの骨に付いている")]
        [SerializeField]
        private Transform _stringNockBone;

        [Tooltip(
            "つがえた矢が通る点（弓Prefab内の空GO。弓の中央の矢を乗せる位置に置く）。矢は弦からこの点へ向く。矢の上方向はこの点の上(緑の軸)"
        )]
        [FormerlySerializedAs("_arrowNockPoint")]
        [SerializeField]
        private Transform _arrowRestPoint;

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
        private Camera _mainCamera;
        private BowState _currentState;
        private GameObject _nockedArrow;
        private BowStringPull _stringPull;
        private int _upperBodyLayer;

        // 照準の Raycast が当たる対象。プレイヤー自身(カメラとの間に入りうる)と、飛んでいる・刺さった矢は除く
        private int _aimRayMask;

        // --- ステート(BowStates.cs)から使う参照と状態 ---
        public PlayerInputHandler Input { get; private set; }
        public PlayerController Player { get; private set; }
        public Animator Animator { get; private set; }

        /// <summary>つがえた矢が通る点。</summary>
        public Transform ArrowRestPoint => _arrowRestPoint;

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

            _aimRayMask =
                Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "PlayerAttack");

            Assert.IsTrue(
                _upperBodyLayer >= 0,
                $"{name}: Animator に「{PlayerAnimatorStates.UpperBodyLayerName}」レイヤーがありません"
            );
            Assert.IsNotNull(_owner, $"{name}: 親階層に PlayerStatus が見つかりません");
            Assert.IsNotNull(_stringNockBone, $"{name}: _stringNockBone が未設定です");
            Assert.IsNotNull(_arrowRestPoint, $"{name}: _arrowRestPoint が未設定です");

            _stringPull = new BowStringPull(_stringNockBone);
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            Assert.IsNotNull(_mainCamera, $"{name}: MainCamera タグのカメラが見つかりません");
            Assert.IsNotNull(ArrowPool.Instance, $"{name}: シーンに ArrowPool がありません");
        }

        private void OnEnable()
        {
            ChangeState(new BowStateFree(this));
            // 弓を持っている間は、構えていない時の歩きを弓用にする
            Animator.SetFloat(PlayerAnimatorParams.BowEquipped, 1f);
        }

        private void OnDisable()
        {
            Animator.SetFloat(PlayerAnimatorParams.BowEquipped, 0f);

            // 武器をしまったら現在の行動を強制終了し、プレイヤーのロックを解除する
            _currentState.Exit();
            Player.IsAiming = false;
            Player.Locks.Release(this);

            // 弓のトリガーが残ったまま別武器に切り替わると誤発動するためリセット
            Animator.ResetTrigger(PlayerAnimatorParams.Fire);

            DestroyArrow();
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
        }

        // アニメーションで手が動いた後に、弦と矢を引き手に合わせる
        private void LateUpdate()
        {
            _stringPull.Apply();
            if (IsArrowNocked)
                PlaceNockedArrow();
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

        /// <summary>
        /// UpperBody レイヤーで指定のステートを再生中か、そこへ遷移中なら true を返し、そのステート自身の情報を info に入れる。
        /// そのステートから抜けている遷移の最中は false(抜けかけの古い再生を「入った」と数えない)。
        ///
        /// 遷移中は current 側が遷移元なので、遷移先の情報は next 側から取る。
        /// 遷移元の再生位置を見ると、長く再生されていた遷移元(待機の None など)の値で「最後まで再生した」と誤判定する。
        /// </summary>
        public bool TryGetUpperBodyState(int stateHash, out AnimatorStateInfo info)
        {
            if (Animator.IsInTransition(_upperBodyLayer))
            {
                info = Animator.GetNextAnimatorStateInfo(_upperBodyLayer);
                return info.shortNameHash == stateHash;
            }

            info = Animator.GetCurrentAnimatorStateInfo(_upperBodyLayer);
            return info.shortNameHash == stateHash;
        }

        /// <summary>矢を持っているか(手元・つがえ済みのどちらでも)。</summary>
        public bool HasArrow => _nockedArrow != null;

        /// <summary>引き手に矢を出す(GenerateArrow イベント)。既に持っていれば何もしない。</summary>
        public void GenerateArrow()
        {
            if (_nockedArrow != null)
                return;

            ArrowProjectile arrow = ArrowPool.Instance.Get();
            arrow.transform.SetParent(_arrowHandHoldPoint);
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            _nockedArrow = arrow.gameObject;
        }

        /// <summary>手元の矢を弦につがえ、以後は弦を引き手に付けて引く。発射可能になる(DrawComplete イベント)。</summary>
        public void NockArrow()
        {
            if (_nockedArrow == null || IsArrowNocked)
                return;

            // 位置と向きは毎フレーム PlaceNockedArrow で決めるので、見た目の大きさだけ保って弓の子にする
            _nockedArrow.transform.SetParent(transform, true);
            _stringPull.StartPull(_arrowHandHoldPoint);
            IsArrowNocked = true;
            _stringPull.Apply();
            PlaceNockedArrow();
            WorldSoundPlayer.Play(_drawSound, transform.position, this);
        }

        /// <summary>つがえた矢の尾を弦の引き点に掛け、弓の矢の通り道へ向ける。</summary>
        private void PlaceNockedArrow()
        {
            Vector3 nock = _stringPull.NockPosition;
            Vector3 toRest = _arrowRestPoint.position - nock;
            Quaternion rotation =
                toRest.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(toRest, _arrowRestPoint.up)
                    : _nockedArrow.transform.rotation;
            _nockedArrow.transform.SetPositionAndRotation(nock, rotation);
        }

        public void FireArrow()
        {
            if (_nockedArrow == null)
                return;

            Vector3 direction = GetShootDirection();
            _stringPull.Release();
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
            _stringPull.Release();
        }

        /// <summary>
        /// 画面中央（クロスヘアー位置）からRaycastして実際のワールド上の着弾点を求め、
        /// つがえた矢の位置からその着弾点への方向ベクトルを返す。
        /// これにより三人称視点でもカメラが向いている方向に矢が正確に飛ぶ。
        /// </summary>
        private Vector3 GetShootDirection()
        {
            Ray ray = _mainCamera.ViewportPointToRay(new Vector2(0.5f, 0.5f));
            // トリガー(ドアの開閉判定など)は見えない壁なので照準から外す。当たると矢があらぬ方向へ飛ぶ
            Vector3 targetPoint = Physics.Raycast(
                ray,
                out RaycastHit hit,
                _maxRange,
                _aimRayMask,
                QueryTriggerInteraction.Ignore
            )
                ? hit.point
                : ray.GetPoint(_maxRange);

            Vector3 dir = targetPoint - _nockedArrow.transform.position;
            // 着弾点と矢の位置がほぼ一致する場合（ゼロ除算防止）はカメラの方向をフォールバックとする
            return dir.sqrMagnitude < 0.0001f ? ray.direction : dir.normalized;
        }

        /// <summary>
        /// 怯み・死亡時(WeaponManager.ResetEquippedWeapon 経由)に、弓のステートを待機へ戻す。
        /// 行動制限は owner ごとに管理されるので、待機の Enter で弓の分を外しても怯み側の制限は残る。
        /// </summary>
        public void ForceReset()
        {
            ChangeState(new BowStateFree(this));
        }
    }
}
