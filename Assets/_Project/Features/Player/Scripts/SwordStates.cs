using UnityEngine;

namespace CreativeAI.Gameplay
{
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

    // ① 通常状態（待機・走りなど自由に動ける）
    public class SwordStateFree : SwordState
    {
        public SwordStateFree(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            // Free状態に戻った瞬間に移動と武器切り替えを許可する
            ctx.playerController.CanMove = true;
            ctx.playerController.CanChangeWeapon = true;

            // 待機・移動状態に戻った時点で完全にコンボをリセットする
            ctx.comboStep = 0;
            ctx.guardHitCount = 0;

            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.normalSwordRotation);
            }
        }

        public override void Update()
        {
            // 防御（右クリック）が押されたら防御ステートへ
            if (ctx.input.subAction)
            {
                ctx.ChangeState(new SwordStateGuard(ctx));
                return;
            }

            // 攻撃（左クリック）が押されたら
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

    // ② 防御状態
    public class SwordStateGuard : SwordState
    {
        public SwordStateGuard(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.playerController.CanMove = false;
            ctx.playerController.CanChangeWeapon = false;
            ctx.animator.SetBool("IsGuarding", true);

            // ガード開始時にパリィ受付タイマーをセット
            ctx.parryTimer = ctx.parryWindowDuration;
            ctx.guardHitCount = 0;

            // 剣の角度をガード用に変更
            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.guardSwordRotation);
            }
        }

        public override void Update()
        {
            ctx.input.ConsumeAttack();

            // パリィタイマーの消費
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

            // 剣の角度を元に戻す
            if (ctx.weaponMeshRoot != null)
            {
                ctx.weaponMeshRoot.localRotation = Quaternion.Euler(ctx.normalSwordRotation);
            }
        }
    }

    // ⑤ パリィ成功（弾き返し）状態
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
            // パリィモーション中に攻撃ボタンを押したら、キャンセルして即座に反撃（Attackステートへ）
            if (ctx.input.ConsumeAttack())
            {
                ctx.targetEnemy = ctx.FindNearestEnemy();
                ctx.ChangeState(new SwordStateAttack(ctx));
                return;
            }

            AnimatorStateInfo state = ctx.animator.GetCurrentAnimatorStateInfo(0);

            // アニメーションが "Parry" ステートに入っているか確認
            bool isPlayingParry = state.IsName("Parry");

            // アニメーションが終了したら（あるいは遷移の猶予時間を過ぎたら）元の状態に戻る
            if (!isPlayingParry && !ctx.animator.IsInTransition(0) && Time.time > _startTime + 0.1f)
            {
                // ガードボタンをまだ押しっぱなしなら構え(Guard)に戻る、離していれば通常(Free)に戻る
                if (ctx.input.subAction) // ※環境に合わせてガード入力の変数を指定
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

    // ③ ダッシュ状態（敵への自動接近）
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
                    Time.deltaTime * 15f
                );

            Vector3 beforePos = ctx.playerTransform.position;
            ctx.characterController.Move(dir.normalized * ctx.dashSpeed * Time.deltaTime);
            Vector3 afterPos = ctx.playerTransform.position;

            // 射程内に入った、もしくは敵や壁に引っかかって物理的に前進できなくなった場合は即座に攻撃へ移行する
            // （タイムアウトによる強制移行は廃止しました）
            if (
                dir.magnitude <= ctx.attackRange
                || Vector3.Distance(beforePos, afterPos) < (ctx.dashSpeed * Time.deltaTime * 0.1f)
            )
            {
                ctx.ChangeState(new SwordStateAttack(ctx));
            }
        }
    }

    // ④ 攻撃状態（コンボシーケンス全体）
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
                    // Animatorがダッシュ等のトランジション中でトリガーを取りこぼすのを防ぐため、
                    // 確実に攻撃ステートに遷移し始めるまでトリガーを送り続ける
                    ctx.animator.SetTrigger(_expectedSlashName);

                    // 万が一遷移設定が存在しない場合の無限ループ防止（ダッシュ終了を待つため2秒と長めに設定）
                    if (Time.time - _stateEnterTime > 2.0f)
                    {
                        ctx.comboStep = 0;
                        ctx.ChangeState(new SwordStateFree(ctx));
                    }
                    return; // 攻撃アニメーションが開始されるまでコンボ入力は受け付けない
                }
            }

            // コンボ入力の先行入力受付
            if (ctx.input.ConsumeAttack() && Time.time - _lastTriggerTime > 0.1f)
            {
                _isComboQueued = true;
            }

            // アニメーションが進行し、攻撃判定が終了する直後（目安として0.4f以降）での処理
            // （プロのアクションゲームのように、モーションを早めにキャンセルして次のコンボへ移行します）
            if (_hasStartedAction && isPlayingSlash && state.normalizedTime > 0.3f)
            {
                if (_isComboQueued && ctx.comboStep < 3)
                {
                    // 以前のターゲットがまだ生きていれば維持する（見失い防止）
                    if (ctx.targetEnemy != null && !ctx.targetEnemy.gameObject.activeInHierarchy)
                    {
                        ctx.targetEnemy = null;
                    }
                    if (ctx.targetEnemy == null)
                    {
                        ctx.targetEnemy = ctx.FindNearestEnemy();
                    }

                    // アニメーション終盤の「今」の距離を評価してダッシュするか判断する
                    if (
                        ctx.targetEnemy != null
                        && Vector3.Distance(ctx.playerTransform.position, ctx.targetEnemy.position)
                            > ctx.attackRange + 0.5f
                    )
                    {
                        ctx.ChangeState(new SwordStateDash(ctx));
                        return;
                    }

                    // その場で次のコンボへ
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
                    // コンボ入力がなく、移動入力がある場合はリカバリーモーションをキャンセルして即座に動けるようにする
                    ctx.comboStep = 0;
                    ctx.ChangeState(new SwordStateFree(ctx));
                    return;
                }
            }

            // 攻撃アニメーションが完全に終了し、別のステート（Idleや移動等）への遷移も終わったら完了
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
