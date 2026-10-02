using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接武器のステートの基底クラス。剣・鎌で共通の Dash / Attack と、各武器固有のステートの親。
    /// </summary>
    public abstract class MeleeWeaponState
    {
        protected MeleeWeaponController ctx;

        protected MeleeWeaponState(MeleeWeaponController context)
        {
            ctx = context;
        }

        /// <summary>
        /// 中立状態(怯み・非装備時に戻す先)か。true のステートにいる間は、
        /// 怯み/非装備の検知で毎フレーム作り直されない。
        /// </summary>
        public virtual bool IsNeutral => false;

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }

        /// <summary>クリップの AnimationEvent を受け取る。自分が再生中のステート由来かは受け側で判定する。</summary>
        public virtual void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        ) { }
    }

    /// <summary>
    /// ダッシュステート（敵への自動接近）。
    /// 攻撃範囲外の敵に対して自動で接近し、射程内に入るか壁等で前進不能になった時点で攻撃に移行する。
    /// </summary>
    public class MeleeStateDash : MeleeWeaponState
    {
        public MeleeStateDash(MeleeWeaponController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.LockControls();
            ctx.StartAnimatorAction(PlayerAnimatorParams.Approach);
        }

        public override void Update()
        {
            if (ctx.TargetEnemy == null)
            {
                ctx.ReturnAnimatorToLocomotion();
                ctx.ChangeState(ctx.CreateNeutralState());
                return;
            }

            Vector3 dir = ctx.TargetEnemy.position - ctx.PlayerTransform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                ctx.PlayerTransform.rotation = Quaternion.Slerp(
                    ctx.PlayerTransform.rotation,
                    Quaternion.LookRotation(dir),
                    // 15fは高速な回転補間。ダッシュ中に敵の方向を向き続けるため速めに設定
                    Time.deltaTime * 15f
                );

            Vector3 beforePos = ctx.PlayerTransform.position;
            ctx.CharacterController.Move(dir.normalized * ctx.dashSpeed * Time.deltaTime);
            Vector3 afterPos = ctx.PlayerTransform.position;

            // 射程内に入った、または壁等に引っかかって物理的に前進不能になった場合に攻撃へ移行。
            // 移動距離が期待値の10%以下なら「前進不能」と判定する
            if (
                dir.magnitude <= ctx.attackRange
                || Vector3.Distance(beforePos, afterPos) < (ctx.dashSpeed * Time.deltaTime * 0.1f)
            )
            {
                ctx.ChangeState(new MeleeStateAttack(ctx));
            }
        }
    }

    /// <summary>
    /// 攻撃ステート（3段コンボシーケンス）。
    ///
    /// タイミングはすべて再生中の攻撃クリップ(Attack1〜3 ステート)の AnimationEvent で決まる:
    /// - TriggerEnableHitbox / TriggerDisableHitbox : 攻撃判定の ON / OFF
    /// - ComboWindowOpen : 以降、先行入力済みなら次段へ繋ぐ(1・2段目に必要。3段目は不要)
    /// - RecoveryStart   : 以降、移動入力で硬直をキャンセルして操作に戻れる
    /// - ActionEnd       : 攻撃の終わり。操作を返し、移動モーションへ戻す
    /// ActionEnd が無いまま攻撃ステートを抜けた場合も終了する(設定漏れとして警告)。
    /// </summary>
    public class MeleeStateAttack : MeleeWeaponState
    {
        // ダブルクリック1回で2段分の入力を消費しないための最小間隔。アニメーションのタイミングではなく入力の間引き。
        private const float MinComboInputInterval = 0.1f;

        // Trigger を送ったのに攻撃ステートへ入れない(遷移の設定漏れ等)ときに待機へ戻すまでの猶予。
        // ダッシュからの遷移完了を待つため長めに取る。
        private const float EnterAttackTimeout = 2.0f;

        private int _expectedStateHash;
        private float _lastTriggerTime;
        private float _attackRequestTime;
        private bool _hasStartedAction;
        private bool _isComboQueued;
        private bool _isComboWindowOpen;
        private bool _isRecoveryStarted;
        private bool _isActionEnded;

        public MeleeStateAttack(MeleeWeaponController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.LockControls();

            ctx.ComboStep++;
            if (ctx.ComboStep > MeleeWeaponController.MaxComboStep)
                ctx.ComboStep = 1;

            RequestAttack();
            ctx.OnAttackStateEntered();
        }

        public override void Exit()
        {
            // 遷移の割り込み等で TriggerDisableHitbox が飛ばされても判定が残らないようにする
            ctx.SetHitboxActive(false);
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            // 前段からのブレンド中は前段クリップのイベントも届くため、今の段のものだけ受け付ける
            if (sourceState.shortNameHash != _expectedStateHash)
                return;

            switch (type)
            {
                case WeaponAnimationEventType.HitboxOn:
                    ctx.SetHitboxActive(true);
                    break;
                case WeaponAnimationEventType.HitboxOff:
                    ctx.SetHitboxActive(false);
                    break;
                case WeaponAnimationEventType.ComboWindowOpen:
                    _isComboWindowOpen = true;
                    break;
                case WeaponAnimationEventType.RecoveryStart:
                    _isRecoveryStarted = true;
                    break;
                case WeaponAnimationEventType.ActionEnd:
                    _isActionEnded = true;
                    break;
            }
        }

        public override void Update()
        {
            AnimatorStateInfo state = ctx.Animator.GetCurrentAnimatorStateInfo(0);
            bool isTransitioningToAttack =
                ctx.Animator.IsInTransition(0)
                && ctx.Animator.GetNextAnimatorStateInfo(0).shortNameHash == _expectedStateHash;
            bool isPlayingAttack = state.shortNameHash == _expectedStateHash;

            if (!_hasStartedAction)
            {
                if (isPlayingAttack || isTransitioningToAttack)
                {
                    _hasStartedAction = true;
                }
                else
                {
                    if (Time.time - _attackRequestTime > EnterAttackTimeout)
                    {
                        Debug.LogWarning(
                            $"[{ctx.name}] {PlayerAnimatorStates.AttackName(ctx.ComboStep)} ステートへ遷移できませんでした。"
                                + "Animator の遷移設定を確認してください。"
                        );
                        ctx.Animator.ResetTrigger(PlayerAnimatorParams.Attacks[ctx.ComboStep - 1]);
                        ctx.ChangeState(ctx.CreateNeutralState());
                    }
                    return;
                }
            }

            if (ctx.Input.ConsumeAttack() && Time.time - _lastTriggerTime > MinComboInputInterval)
            {
                _isComboQueued = true;
            }

            if (_isComboWindowOpen && _isComboQueued && ctx.ComboStep < MeleeWeaponController.MaxComboStep)
            {
                ContinueCombo();
                return;
            }

            if (_isActionEnded)
            {
                FinishAttack();
                return;
            }

            if (_isRecoveryStarted && !_isComboQueued && ctx.Input.move.sqrMagnitude > 0.01f)
            {
                // コンボ入力がなく移動入力がある場合、リカバリーモーションをキャンセルして即座に動けるようにする
                FinishAttack();
                return;
            }

            // ActionEnd を受け取らないまま攻撃ステートを抜けた(遷移の割り込み・イベントの付け忘れ)
            if (!isPlayingAttack && !isTransitioningToAttack && !ctx.Animator.IsInTransition(0))
            {
                Debug.LogWarning(
                    $"[{ctx.name}] {PlayerAnimatorStates.AttackName(ctx.ComboStep)} で ActionEnd イベントを受信しないまま攻撃が終了しました。"
                        + "攻撃クリップに ActionEnd を追加してください。"
                );
                ctx.ChangeState(ctx.CreateNeutralState());
            }
        }

        private void FinishAttack()
        {
            ctx.ReturnAnimatorToLocomotion();
            ctx.ChangeState(ctx.CreateNeutralState());
        }

        private void ContinueCombo()
        {
            // ターゲットが撃破済み（非アクティブ）なら見失い防止のため再索敵する
            if (ctx.TargetEnemy != null && !ctx.TargetEnemy.gameObject.activeInHierarchy)
            {
                ctx.TargetEnemy = null;
            }
            if (ctx.TargetEnemy == null)
            {
                ctx.TargetEnemy = ctx.FindNearestEnemy();
            }

            // コンボ中に敵が射程外に移動した場合、再度ダッシュで接近する。
            // +0.5fの余裕は、攻撃モーション中の微小な距離変動での不要なダッシュを防ぐため
            if (
                ctx.TargetEnemy != null
                && Vector3.Distance(ctx.PlayerTransform.position, ctx.TargetEnemy.position)
                    > ctx.attackRange + 0.5f
            )
            {
                ctx.ChangeState(new MeleeStateDash(ctx));
                return;
            }

            ctx.SetHitboxActive(false);
            ctx.ComboStep++;
            RequestAttack();
        }

        private void RequestAttack()
        {
            int index = ctx.ComboStep - 1;
            _expectedStateHash = PlayerAnimatorStates.Attacks[index];
            ctx.StartAnimatorAction(PlayerAnimatorParams.Attacks[index]);

            _lastTriggerTime = Time.time;
            _attackRequestTime = Time.time;
            _hasStartedAction = false;
            _isComboQueued = false;
            _isComboWindowOpen = false;
            _isRecoveryStarted = false;
            _isActionEnded = false;
        }
    }
}
