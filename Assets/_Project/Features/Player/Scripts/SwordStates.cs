using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 剣のステートマシンの基底クラス。
    /// Free → Guard/Dash/Attack の遷移を管理し、
    /// パリィ成功時はGuardまたはParry → Attack への反撃遷移も可能。
    /// </summary>
    public abstract class SwordState
    {
        protected SwordController ctx;

        public SwordState(SwordController context)
        {
            ctx = context;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }
    }

    /// <summary>
    /// 待機ステート。コンボやガードの状態を完全にリセットし、
    /// 攻撃または防御の入力を待つ。
    /// </summary>
    public class SwordStateFree : SwordState
    {
        public SwordStateFree(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = true;
            ctx.playerController.CanChangeWeapon = true;

            ctx.comboStep = 0;
            ctx.guardHitCount = 0;

            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.normalSwordRotation);
            }
        }

        public override void Update()
        {
            if (ctx.input.subAction)
            {
                ctx.ChangeState(new SwordStateGuard(ctx));
                return;
            }

            if (ctx.input.ConsumeAttack())
            {
                ctx.targetEnemy = ctx.FindNearestEnemy();
                if (
                    ctx.targetEnemy != null
                    && Vector3.Distance(ctx.playerTransform.position, ctx.targetEnemy.position)
                        > ctx.attackRange
                )
                    ctx.ChangeState(new SwordStateDash(ctx));
                else
                    ctx.ChangeState(new SwordStateAttack(ctx));
            }
        }
    }

    /// <summary>
    /// 防御ステート。subAction押下中はガード姿勢を維持する。
    /// ガード開始直後にパリィ受付タイマーが作動し、受付時間内の被弾はジャストパリィとなる。
    /// </summary>
    public class SwordStateGuard : SwordState
    {
        public SwordStateGuard(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = false;
            ctx.playerController.CanChangeWeapon = false;
            ctx.animator.SetBool("IsGuarding", true);

            ctx.parryTimer = ctx.parryWindowDuration;
            ctx.guardHitCount = 0;

            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.guardSwordRotation);
            }
        }

        public override void Update()
        {
            ctx.input.ConsumeAttack();

            if (ctx.parryTimer > 0f)
            {
                ctx.parryTimer -= Time.deltaTime;
            }

            if (!ctx.input.subAction)
            {
                ctx.ChangeState(new SwordStateFree(ctx));
            }
        }

        public override void Exit()
        {
            ctx.animator.SetBool("IsGuarding", false);

            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.normalSwordRotation);
            }
        }
    }

    /// <summary>
    /// パリィ成功（弾き返し）ステート。
    /// パリィモーション中に攻撃入力があれば、即座に反撃としてAttackステートへ遷移する。
    /// </summary>
    public class SwordStateParry : SwordState
    {
        private float _startTime;

        public SwordStateParry(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = false;
            ctx.playerController.CanChangeWeapon = false;

            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.attackSwordRotation);
            }

            // ★注意：Animatorに「Parry」というTriggerパラメータを作成し、
            // 弾き返しアニメーションのステート名も「Parry」に設定してください。
            ctx.animator.SetTrigger("Parry");

            _startTime = Time.time;
        }

        public override void Update()
        {
            // パリィモーション中の攻撃入力で即反撃へ移行（キャンセル攻撃）
            if (ctx.input.ConsumeAttack())
            {
                ctx.targetEnemy = ctx.FindNearestEnemy();
                ctx.ChangeState(new SwordStateAttack(ctx));
                return;
            }

            AnimatorStateInfo state = ctx.animator.GetCurrentAnimatorStateInfo(0);

            bool isPlayingParry = state.IsName("Parry");

            // 0.1fの猶予は、Animatorの遷移にかかるフレーム数分だけ待つため
            if (!isPlayingParry && !ctx.animator.IsInTransition(0) && Time.time > _startTime + 0.1f)
            {
                // ガードボタンを押しっぱなしならGuardに戻り、離していればFreeに戻る
                if (ctx.input.subAction)
                {
                    ctx.ChangeState(new SwordStateGuard(ctx));
                }
                else
                {
                    ctx.ChangeState(new SwordStateFree(ctx));
                }
            }
        }

        public override void Exit()
        {
            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.normalSwordRotation);
            }
        }
    }

    /// <summary>
    /// ダッシュステート（敵への自動接近）。
    /// 攻撃範囲外の敵に対して自動で接近し、射程内に入るか壁等で前進不能になった時点で攻撃に移行する。
    /// </summary>
    public class SwordStateDash : SwordState
    {
        public SwordStateDash(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = false;
            ctx.playerController.CanChangeWeapon = false;
            ctx.animator.SetTrigger("DashTrigger");
        }

        public override void Update()
        {
            if (ctx.targetEnemy == null)
            {
                ctx.ChangeState(new SwordStateFree(ctx));
                return;
            }

            Vector3 dir = ctx.targetEnemy.position - ctx.playerTransform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                ctx.playerTransform.rotation = Quaternion.Slerp(
                    ctx.playerTransform.rotation,
                    Quaternion.LookRotation(dir),
                    // 15fは高速な回転補間。ダッシュ中に敵の方向を向き続けるため速めに設定
                    Time.deltaTime * 15f
                );

            Vector3 beforePos = ctx.playerTransform.position;
            ctx.characterController.Move(dir.normalized * ctx.dashSpeed * Time.deltaTime);
            Vector3 afterPos = ctx.playerTransform.position;

            // 射程内に入った、または壁等に引っかかって物理的に前進不能になった場合に攻撃へ移行。
            // 移動距離が期待値の10%以下なら「前進不能」と判定する
            if (
                dir.magnitude <= ctx.attackRange
                || Vector3.Distance(beforePos, afterPos) < (ctx.dashSpeed * Time.deltaTime * 0.1f)
            )
            {
                ctx.ChangeState(new SwordStateAttack(ctx));
            }
        }
    }

    /// <summary>
    /// 攻撃ステート（3段コンボシーケンス）。
    /// 先行入力を受け付け、アニメーション後半でコンボをキャンセルして次段へ繋ぐ。
    /// 移動入力があればリカバリーモーションをキャンセルして即座に操作に復帰できる。
    /// </summary>
    public class SwordStateAttack : SwordState
    {
        private float _lastTriggerTime;
        private bool _hasStartedAction;
        private float _stateEnterTime;
        private string _expectedSlashName;
        private bool _isComboQueued;

        public SwordStateAttack(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = false;
            ctx.playerController.CanChangeWeapon = false;
            ctx.lastAttackTime = Time.time;

            ctx.comboStep++;
            if (ctx.comboStep > 3)
                ctx.comboStep = 1;

            _expectedSlashName = "Slash" + ctx.comboStep;
            ctx.animator.SetTrigger(_expectedSlashName);

            _lastTriggerTime = Time.time;
            _stateEnterTime = Time.time;
            _hasStartedAction = false;
            _isComboQueued = false;
            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.attackSwordRotation);
            }
        }

        public override void Update()
        {
            AnimatorStateInfo state = ctx.animator.GetCurrentAnimatorStateInfo(0);
            bool isTransitioningToSlash =
                ctx.animator.IsInTransition(0)
                && ctx.animator.GetNextAnimatorStateInfo(0).IsName(_expectedSlashName);
            bool isPlayingSlash = state.IsName(_expectedSlashName);

            if (!_hasStartedAction)
            {
                if (isPlayingSlash || isTransitioningToSlash)
                {
                    _hasStartedAction = true;
                }
                else
                {
                    // ダッシュ等のトランジション中にトリガーが消費されてしまう場合があるため、
                    // 攻撃ステートに確実に遷移するまでトリガーを送り続ける
                    ctx.animator.SetTrigger(_expectedSlashName);

                    // 遷移設定が存在しない場合の無限ループ防止（ダッシュ完了を待つため2秒と長めに設定）
                    if (Time.time - _stateEnterTime > 2.0f)
                    {
                        ctx.comboStep = 0;
                        ctx.ChangeState(new SwordStateFree(ctx));
                    }
                    return;
                }
            }

            // 先行入力の受付。0.1秒の最小間隔はダブルクリックによる二重消費を防ぐため
            if (ctx.input.ConsumeAttack() && Time.time - _lastTriggerTime > 0.1f)
            {
                _isComboQueued = true;
            }

            // normalizedTime > 0.3f で攻撃判定が終了した直後からコンボ入力を処理する。
            // モーションを早めにキャンセルすることで、テンポの良いコンボ体験を実現する
            if (_hasStartedAction && isPlayingSlash && state.normalizedTime > 0.3f)
            {
                if (_isComboQueued && ctx.comboStep < 3)
                {
                    // ターゲットが撃破済み（非アクティブ）なら見失い防止のため再索敵する
                    if (ctx.targetEnemy != null && !ctx.targetEnemy.gameObject.activeInHierarchy)
                    {
                        ctx.targetEnemy = null;
                    }
                    if (ctx.targetEnemy == null)
                    {
                        ctx.targetEnemy = ctx.FindNearestEnemy();
                    }

                    // コンボ中に敵が射程外に移動した場合、再度ダッシュで接近する。
                    // +0.5fの余裕は、攻撃モーション中の微小な距離変動での不要なダッシュを防ぐため
                    if (
                        ctx.targetEnemy != null
                        && Vector3.Distance(ctx.playerTransform.position, ctx.targetEnemy.position)
                            > ctx.attackRange + 0.5f
                    )
                    {
                        ctx.ChangeState(new SwordStateDash(ctx));
                        return;
                    }

                    ctx.comboStep++;
                    _expectedSlashName = "Slash" + ctx.comboStep;
                    ctx.animator.SetTrigger(_expectedSlashName);
                    _lastTriggerTime = Time.time;
                    ctx.lastAttackTime = Time.time;
                    _hasStartedAction = false;
                    _stateEnterTime = Time.time;
                    _isComboQueued = false;
                    return;
                }
                else if (!_isComboQueued && ctx.input.move.sqrMagnitude > 0.01f)
                {
                    // コンボ入力がなく移動入力がある場合、リカバリーモーションをキャンセルして即座に動けるようにする
                    ctx.comboStep = 0;
                    ctx.ChangeState(new SwordStateFree(ctx));
                    return;
                }
            }

            // 攻撃アニメーションが完全に終了し、次のステートへの遷移も完了した場合
            if (
                _hasStartedAction
                && !isPlayingSlash
                && !isTransitioningToSlash
                && !ctx.animator.IsInTransition(0)
            )
            {
                ctx.comboStep = 0;
                ctx.ChangeState(new SwordStateFree(ctx));
            }
        }
    }
}
