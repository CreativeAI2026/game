using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 鎌固有のステートの基底クラス。
    /// Free → Throw → WaitReturn → Free の投擲サイクルを管理する。攻撃は共通の Dash / Attack を使う。
    /// </summary>
    public abstract class ScytheState : MeleeWeaponState
    {
        protected readonly ScytheController scythe;

        protected ScytheState(ScytheController context)
            : base(context)
        {
            scythe = context;
        }
    }

    /// <summary>待機ステート。攻撃入力で接近/コンボ、subAction の押下で投擲へ。</summary>
    public class ScytheStateFree : ScytheState
    {
        public ScytheStateFree(ScytheController context)
            : base(context) { }

        public override bool IsNeutral => true;

        public override void Enter()
        {
            scythe.ReleaseControlLocks();
        }

        public override void Update()
        {
            // 投擲・攻撃は地上でのみ始める(空中の攻撃入力は消費せず先行入力として残す)
            if (!scythe.CanStartGroundAction)
                return;

            if (scythe.SubActionPressedThisFrame)
            {
                scythe.ChangeState(new ScytheStateThrow(scythe));
                return;
            }

            if (scythe.Input.ConsumeAttack())
            {
                scythe.BeginAttack();
            }
        }
    }

    /// <summary>
    /// 投げモーションのステート。最寄りの敵(いなければ正面)を向いて投げモーションを再生し、
    /// クリップの ThrowRelease イベントのフレームで鎌を手放す。
    /// </summary>
    public class ScytheStateThrow : ScytheState
    {
        // Trigger を送ったのに Throw ステートへ入れない(遷移の設定漏れ等)ときに待機へ戻すまでの猶予
        private const float EnterThrowTimeout = 2.0f;

        private static readonly int ThrowHash = PlayerAnimatorStates.Throw;

        private Transform _target;
        private float _requestTime;
        private bool _hasStartedAction;

        public ScytheStateThrow(ScytheController context)
            : base(context) { }

        public override void Enter()
        {
            scythe.LockControls();
            scythe.ComboStep = 0;

            _target = scythe.FindNearestEnemy();
            FacePlayerTo(scythe.ResolveThrowDirection(_target));

            scythe.StartAnimatorAction(PlayerAnimatorParams.Throw);
            _requestTime = Time.time;
            _hasStartedAction = false;
        }

        public override void Exit()
        {
            // 手放す前に怯み等で中断された場合、Trigger が残って後から投げモーションが出るのを防ぐ
            scythe.Animator.ResetTrigger(PlayerAnimatorParams.Throw);
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            if (
                type == WeaponAnimationEventType.ThrowRelease
                && sourceState.shortNameHash == ThrowHash
            )
            {
                Release();
            }
        }

        public override void Update()
        {
            // 投擲中の攻撃入力は捨てる(戻った直後に勝手に攻撃が出ないように)
            scythe.Input.ConsumeAttack();

            AnimatorStateInfo state = scythe.Animator.GetCurrentAnimatorStateInfo(0);
            bool isPlayingThrow = state.shortNameHash == ThrowHash;
            bool isTransitioningToThrow =
                scythe.Animator.IsInTransition(0)
                && scythe.Animator.GetNextAnimatorStateInfo(0).shortNameHash == ThrowHash;

            if (!_hasStartedAction)
            {
                if (isPlayingThrow || isTransitioningToThrow)
                {
                    _hasStartedAction = true;
                }
                else if (Time.time - _requestTime > EnterThrowTimeout)
                {
                    Debug.LogWarning(
                        $"[{scythe.name}] Throw ステートへ遷移できませんでした。Animator の遷移設定を確認してください。"
                    );
                    scythe.Animator.ResetTrigger(PlayerAnimatorParams.Throw);
                    scythe.ChangeState(new ScytheStateFree(scythe));
                }
                return;
            }

            // ThrowRelease を受け取らないまま投げモーションが終わった = クリップにイベントが無い。
            // 操作不能のまま固まらないよう、ここで手放して警告する
            if (!isPlayingThrow && !isTransitioningToThrow)
            {
                Debug.LogWarning(
                    $"[{scythe.name}] Throw で ThrowRelease イベントを受信しないままモーションが終了しました。"
                        + "クリップに ThrowRelease イベントが設定されているか確認してください。"
                );
                Release();
            }
        }

        private void Release()
        {
            // 投げモーション中に敵が動いた分を追従するため、方向は手放す瞬間に決め直す
            Vector3 direction = scythe.ResolveThrowDirection(_target);
            FacePlayerTo(direction);
            scythe.LaunchBoomerang(direction);
            scythe.ChangeState(new ScytheStateWaitReturn(scythe));
        }

        private void FacePlayerTo(Vector3 horizontalDirection)
        {
            scythe.PlayerTransform.rotation = Quaternion.LookRotation(horizontalDirection);
        }
    }

    /// <summary>
    /// 投げた鎌が戻ってくるのを待つステート。移動・攻撃・武器切替は不可。
    /// 鎌が手元に戻ったら待機へ。
    /// </summary>
    public class ScytheStateWaitReturn : ScytheState
    {
        public ScytheStateWaitReturn(ScytheController context)
            : base(context) { }

        /// <summary>鎌が飛んでいる間の中立状態(怯み明けの戻り先)。</summary>
        public override bool IsNeutral => true;

        public override void Enter()
        {
            scythe.LockControls();
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            // 投げモーションの終わりで移動モーション(待機ポーズ)へ戻す。操作は鎌を受け取るまで戻さない
            if (
                type == WeaponAnimationEventType.ActionEnd
                && sourceState.shortNameHash == PlayerAnimatorStates.Throw
            )
                scythe.ReturnAnimatorToLocomotion();
        }

        public override void Update()
        {
            scythe.Input.ConsumeAttack();

            if (!scythe.IsBoomerangFlying)
            {
                scythe.ChangeState(new ScytheStateFree(scythe));
            }
        }
    }
}
