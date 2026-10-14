using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接武器(剣・鎌)の共通基盤。ステートマシンの駆動と、共通の「敵への接近 → 3段コンボ」を担う。
    /// 武器固有の行動(剣のガード/パリィ、鎌の投擲)は派生クラスと、その派生クラス専用のステートが持つ。
    ///
    /// 攻撃のタイミング(判定ON/OFF・コンボ受付・硬直キャンセル)はクリップの AnimationEvent で決まる。
    /// イベントは <see cref="PlayerAnimationEventRelay"/> 経由で <see cref="OnWeaponAnimationEvent"/> に届き、
    /// 現在のステートへ渡される。
    /// </summary>
    public abstract class MeleeWeaponController
        : MonoBehaviour,
            IWeaponAnimationEventHandler,
            IResettableWeapon
    {
        public const int MaxComboStep = 3;

        [Header("設定")]
        [Tooltip(
            "攻撃・投擲の狙いを探す半径（m）。敵のレイヤーは PlayerTargetManager で一元管理する"
        )]
        public float searchRadius = 8f;
        public float attackRange = 3f;
        public float dashSpeed = 20f;

        [Header("攻撃判定")]
        [Tooltip(
            "この武器の子にある PlayerMeleeHitbox。AnimationEvent の TriggerEnableHitbox / TriggerDisableHitbox で ON/OFF する"
        )]
        [SerializeField]
        private PlayerMeleeHitbox _hitbox;

        // --- 実行時に Awake で取得する参照・状態(シーンに保存されないようプロパティにしている) ---
        public PlayerInputHandler Input { get; private set; }
        public Animator Animator { get; private set; }
        public PlayerController Player { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public CharacterController CharacterController { get; private set; }

        /// <summary>接近・攻撃の対象。ステートが更新する。</summary>
        public Transform TargetEnemy { get; set; }

        /// <summary>3段コンボの現在の段(0 = 攻撃していない)。ステートが更新する。</summary>
        public int ComboStep { get; set; }

        protected WeaponManager weaponManager;
        private PlayerTargetManager _targetManager;

        private MeleeWeaponState _currentState;
        private bool _prevSubAction;

        protected MeleeWeaponState CurrentState => _currentState;

        /// <summary>subAction ボタンがこのフレームで押された瞬間か(押しっぱなしは false)。</summary>
        public bool SubActionPressedThisFrame { get; private set; }

        /// <summary>
        /// 武器の中立状態(怯み・非装備時・ForceReset 時に戻す先)を生成する。
        /// 剣なら待機、鎌なら「投げた鎌が戻るまでは帰還待ち」など、武器の状況で変わりうる。
        /// </summary>
        public abstract MeleeWeaponState CreateNeutralState();

        protected virtual void Awake()
        {
            var root = GetComponentInParent<PlayerController>();
            Assert.IsNotNull(root, $"{name}: 親階層に PlayerController が見つかりません");
            PlayerTransform = root.transform;
            Input = root.GetComponent<PlayerInputHandler>();
            Animator = PlayerModel.Of(this).Animator;
            CharacterController = root.GetComponent<CharacterController>();
            Player = root;
            weaponManager = root.GetComponent<WeaponManager>();
            _targetManager = root.GetComponent<PlayerTargetManager>();

            Assert.IsNotNull(Input, $"{name}: PlayerInputHandler が見つかりません");
            Assert.IsNotNull(weaponManager, $"{name}: WeaponManager が見つかりません");
            Assert.IsNotNull(_targetManager, $"{name}: PlayerTargetManager が見つかりません");
            Assert.IsNotNull(_hitbox, $"{name}: 攻撃判定(PlayerMeleeHitbox)が未設定です");
        }

        protected virtual void OnEnable()
        {
            ChangeState(CreateNeutralState());
        }

        protected virtual void OnDisable()
        {
            // 他の武器に切り替わった際に攻撃トリガーが残ると誤発動するためリセット
            foreach (int attack in PlayerAnimatorParams.Attacks)
                Animator.ResetTrigger(attack);
            Animator.ResetTrigger(PlayerAnimatorParams.Approach);

            _currentState.Exit();
            SetHitboxActive(false);

            Player.Locks.Release(this);
        }

        protected virtual void Update()
        {
            // 怯み中、または掴まれ中は武器ステートマシンを完全停止し、入力を一切処理しない
            if (Player.IsFlinching || Player.IsGrabbed)
            {
                ReturnToNeutral();
                return;
            }

            // 非装備時に裏で入力を消費するのを防ぐ。
            // 番号ではなく「選択中の武器オブジェクトが自分か」で判定するので、Inspector で番号を合わせる必要はない
            if (weaponManager.CurrentWeaponObject != gameObject)
            {
                ReturnToNeutral();
                return;
            }

            SubActionPressedThisFrame = Input.subAction && !_prevSubAction;
            _prevSubAction = Input.subAction;

            OnBeforeStateUpdate();

            _currentState.Update();
        }

        /// <summary>ステート更新の直前に毎フレーム呼ばれる(装備中かつ操作可能なときのみ)。</summary>
        protected virtual void OnBeforeStateUpdate() { }

        private void ReturnToNeutral()
        {
            if (!_currentState.IsNeutral)
                ChangeState(CreateNeutralState());
        }

        public void ChangeState(MeleeWeaponState newState)
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

        public void SetHitboxActive(bool active)
        {
            if (active)
                _hitbox.EnableHitbox();
            else
                _hitbox.DisableHitbox();
        }

        /// <summary>
        /// 攻撃入力を受けたときの共通処理。最寄りの敵が射程外ならダッシュで接近、射程内(または敵なし)なら即攻撃。
        /// </summary>
        public void BeginAttack()
        {
            TargetEnemy = FindNearestEnemy();
            if (
                TargetEnemy != null
                && Vector3.Distance(PlayerTransform.position, TargetEnemy.position) > attackRange
            )
                ChangeState(new MeleeStateDash(this));
            else
                ChangeState(new MeleeStateAttack(this));
        }

        /// <summary>
        /// 中立状態に入るときの共通処理。この武器が掛けた行動制限を外し、コンボ段数を戻す。
        /// 掴まれ・怯みなど他が掛けた制限はそれぞれが管理するので触らない。
        /// </summary>
        public void ReleaseControlLocks()
        {
            Player.Locks.Release(this);
            ComboStep = 0;
        }

        /// <summary>攻撃・投擲などの行動中に、移動・ジャンプ・武器切替を禁止する。</summary>
        public void LockControls()
        {
            Player.Locks.Set(this, PlayerLock.All);
        }

        /// <summary>
        /// 攻撃・投擲などの行動を始める Trigger を送る。
        /// 直前の行動の「移動へ戻る」指示が残っていると、行動ステートに入った直後に抜けてしまうので先に消す。
        /// </summary>
        public void StartAnimatorAction(int triggerHash)
        {
            Animator.ResetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            Animator.SetTrigger(triggerHash);
        }

        /// <summary>行動ステートから移動モーションへ戻す(ActionEnd・硬直キャンセル時)。</summary>
        public void ReturnAnimatorToLocomotion()
        {
            Animator.SetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
        }

        /// <summary>
        /// 地上にいて新しい行動(攻撃・投擲)を始められるか。
        /// 空中では行動ステートを使わないので、入力は消費せず先行入力として残す(着地後、期限内なら出る)。
        /// </summary>
        public bool CanStartGroundAction => Player.Grounded;

        /// <summary>攻撃ステートに入った瞬間に呼ばれる。武器の見た目の調整などに使う。</summary>
        public virtual void OnAttackStateEntered() { }

        /// <summary>この武器の索敵半径内で最寄りの敵を探す(索敵の本体は PlayerTargetManager)。</summary>
        public Transform FindNearestEnemy() => _targetManager.FindNearest(searchRadius);

        /// <summary>
        /// 怯み・死亡時(WeaponManager.ResetEquippedWeapon 経由)に、現在のステートを終了して中立状態へ戻す。
        /// 行動制限は owner ごとに管理されるので、中立状態の Enter で自分の分を戻しても怯み側の制限は残る。
        /// </summary>
        public virtual void ForceReset()
        {
            SetHitboxActive(false);
            ChangeState(CreateNeutralState());
        }
    }
}
