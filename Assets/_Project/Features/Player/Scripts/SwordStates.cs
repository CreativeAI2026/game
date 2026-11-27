using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 剣固有のステートの基底クラス。
    /// Free → Guard / (共通の)Dash・Attack の遷移を管理し、
    /// パリィ成功時はGuardまたはParry → Attack への反撃遷移も可能。
    /// </summary>
    public abstract class SwordState : MeleeWeaponState
    {
        protected readonly SwordController sword;

        protected SwordState(SwordController context)
            : base(context)
        {
            sword = context;
        }
    }

    /// <summary>
    /// 待機ステート。コンボやガードの状態を完全にリセットし、
    /// 攻撃または防御の入力を待つ。
    /// </summary>
    public class SwordStateFree : SwordState
    {
        public SwordStateFree(SwordController context)
            : base(context) { }

        public override bool IsNeutral => true;

        public override void Enter()
        {
            sword.ReleaseControlLocks();
        }

        public override void Update()
        {
            // 防御が割れた後は、メーターが一定量戻るまで構えられない
            if (sword.Input.subAction && !sword.GuardGauge.IsBroken(Time.time))
            {
                sword.ChangeState(new SwordStateGuard(sword));
                return;
            }

            // 空中では消費せず先行入力として残す(着地後、期限内なら攻撃が出る)
            if (sword.CanStartGroundAction && sword.Input.ConsumeAttack())
            {
                sword.BeginAttack();
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
            sword.LockControls();
            sword.Animator.SetBool(PlayerAnimatorParams.IsGuarding, true);

            sword.GuardGauge.BeginGuard(Time.time);
        }

        public override void Update()
        {
            sword.Input.ConsumeAttack();

            // 割れたのに怯めなかった場合(掴まれ中など)も、構えたまま残さない
            if (!sword.Input.subAction || sword.GuardGauge.IsBroken(Time.time))
            {
                sword.ChangeState(new SwordStateFree(sword));
            }
        }

        public override void Exit()
        {
            sword.Animator.SetBool(PlayerAnimatorParams.IsGuarding, false);
            sword.GuardGauge.EndGuard(Time.time);
        }
    }

    /// <summary>
    /// パリィ成功（弾き返し）ステート。
    /// パリィモーション中に攻撃入力があれば、即座に反撃としてAttackステートへ遷移する。
    /// 終わりはパリィクリップの ActionEnd イベントで決まる。
    /// </summary>
    public class SwordStateParry : SwordState
    {
        // Trigger を送ったのにパリィステートへ入れない(遷移の設定漏れ等)ときに打ち切るまでの猶予
        private const float EnterParryTimeout = 1.0f;

        private AnimationStateWatch _parryWatch;
        private bool _isActionEnded;

        public SwordStateParry(SwordController context)
            : base(context) { }

        public override void Enter()
        {
            sword.LockControls();

            sword.StartAnimatorAction(PlayerAnimatorParams.Parry);

            _parryWatch.Begin();
            _isActionEnded = false;
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            if (
                type == WeaponAnimationEventType.ActionEnd
                && sourceState.shortNameHash == PlayerAnimatorStates.Parry
            )
                _isActionEnded = true;
        }

        public override void Update()
        {
            // パリィモーション中の攻撃入力で即反撃へ移行（キャンセル攻撃）
            if (sword.Input.ConsumeAttack())
            {
                sword.TargetEnemy = sword.FindMeleeTarget();
                sword.ChangeState(new MeleeStateAttack(sword));
                return;
            }

            if (_isActionEnded)
            {
                Finish();
                return;
            }

            AnimationStateWatch.Progress progress = _parryWatch.Update(
                sword.Animator.IsPlayingOrEntering(0, PlayerAnimatorStates.Parry),
                EnterParryTimeout
            );

            if (progress == AnimationStateWatch.Progress.Left)
            {
                Debug.LogWarning(
                    $"[{sword.name}] パリィクリップの ActionEnd イベントを受信しないままパリィステートを抜けました。"
                        + "クリップに ActionEnd を追加してください。"
                );
                Finish();
            }
            else if (progress == AnimationStateWatch.Progress.TimedOut)
            {
                Debug.LogWarning(
                    $"[{sword.name}] Parry ステートへ遷移できませんでした。Animator の遷移設定を確認してください。"
                );
                sword.Animator.ResetTrigger(PlayerAnimatorParams.Parry);
                Finish();
            }
        }

        private void Finish()
        {
            // ガードボタンを押しっぱなしならGuardに戻り(IsGuarding で Animator もガードへ)、離していればFreeに戻る
            if (sword.Input.subAction)
            {
                sword.ChangeState(new SwordStateGuard(sword));
            }
            else
            {
                sword.ReturnAnimatorToLocomotion();
                sword.ChangeState(new SwordStateFree(sword));
            }
        }
    }
}
