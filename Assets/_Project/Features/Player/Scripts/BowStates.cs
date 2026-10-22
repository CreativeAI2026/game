using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓のステートマシンの基底クラス。
    /// Free → Aim → Fire → Release のサイクルで遷移する。
    /// </summary>
    public abstract class BowState
    {
        // Trigger・Bool を送ったのに UpperBody のステートへ入れない(遷移の設定漏れ等)ときに諦めるまでの猶予
        protected const float EnterStateTimeout = 1.0f;

        protected readonly BowController bow;

        protected BowState(BowController context)
        {
            bow = context;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }

        /// <summary>クリップの AnimationEvent を受け取る。自分が再生中のステート由来かは受け側で判定する。</summary>
        public virtual void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        ) { }
    }

    /// <summary>弓の待機ステート。エイムしていない通常状態。</summary>
    public class BowStateFree : BowState
    {
        public BowStateFree(BowController context)
            : base(context) { }

        public override void Enter()
        {
            bow.Player.IsAiming = false;
            bow.Player.Locks.Release(bow);
            bow.DestroyArrow();
        }

        public override void Update()
        {
            if (bow.Input.subAction)
            {
                bow.ChangeState(new BowStateAim(bow));
            }

            // 待機中に攻撃入力が残っていると、次にAimに入った瞬間に暴発するため消費しておく
            bow.Input.ConsumeAttack();
        }
    }

    /// <summary>
    /// 弓のエイムステート。構えクリップの GenerateArrow で矢を手に出し、DrawComplete で矢をつがえる。
    /// つがえた後に攻撃入力で発射する。
    /// </summary>
    public class BowStateAim : BowState
    {
        private float _enterTime;
        private bool _hasEntered;

        public BowStateAim(BowController context)
            : base(context) { }

        public override void Enter()
        {
            bow.Player.IsAiming = true;
            // 構えたまま移動はできる。武器の持ち替えだけ禁止する
            bow.Player.Locks.Set(bow, PlayerLock.ChangeWeapon);
            _enterTime = Time.time;
            _hasEntered = false;
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            if (sourceState.shortNameHash != PlayerAnimatorStates.Aim)
                return;

            if (type == WeaponAnimationEventType.GenerateArrow)
                bow.GenerateArrow();
            else if (type == WeaponAnimationEventType.DrawComplete)
                NockWithArrow();
        }

        /// <summary>
        /// 矢をつがえる。GenerateArrow が届いていない(矢が無い)ときは、撃てないまま固まらないよう、ここで矢を出して警告する。
        /// </summary>
        private void NockWithArrow()
        {
            if (!bow.HasArrow)
            {
                Debug.LogWarning(
                    $"[{bow.name}] 構えクリップの GenerateArrow イベントを受信しないまま矢をつがえようとしました。"
                        + "構えクリップの DrawComplete より前に GenerateArrow を追加してください。"
                );
                bow.GenerateArrow();
            }
            bow.NockArrow();
        }

        public override void Update()
        {
            if (!bow.Input.subAction)
            {
                // 撃たずに構えを解いた(キャンセル)
                bow.NotifyAimCancelled();
                bow.ChangeState(new BowStateFree(bow));
                return;
            }

            if (!bow.IsArrowNocked)
                CheckDrawEventMissing();

            if (bow.Input.ConsumeAttack() && bow.IsArrowNocked)
            {
                bow.ChangeState(new BowStateFire(bow));
            }
        }

        /// <summary>
        /// DrawComplete が届かないまま構えクリップが最後まで再生された(保持ステートへ移った場合も含む)、
        /// または構えステートに入れなかった場合は、撃てないまま固まらないよう、その時点でつがえて警告する。
        /// </summary>
        private void CheckDrawEventMissing()
        {
            bool isInAim = bow.TryGetUpperBodyState(
                PlayerAnimatorStates.Aim,
                out AnimatorStateInfo aimState
            );
            if (isInAim)
                _hasEntered = true;

            if ((isInAim && aimState.normalizedTime >= 1f) || (!isInAim && _hasEntered))
            {
                Debug.LogWarning(
                    $"[{bow.name}] 構えクリップの DrawComplete イベントを受信しないまま最後まで再生されました。"
                        + "構えクリップに DrawComplete を追加してください。"
                );
                NockWithArrow();
            }
            else if (!_hasEntered && Time.time - _enterTime > EnterStateTimeout)
            {
                Debug.LogWarning(
                    $"[{bow.name}] UpperBody の Aim ステートへ遷移できませんでした。Animator の遷移設定を確認してください。"
                );
                NockWithArrow();
            }
        }
    }

    /// <summary>弓の発射ステート。即座に矢を放ち、Release へ遷移する一瞬のステート。</summary>
    public class BowStateFire : BowState
    {
        public BowStateFire(BowController context)
            : base(context) { }

        public override void Enter()
        {
            bow.FireArrow();
            bow.ChangeState(new BowStateRelease(bow));
        }
    }

    /// <summary>
    /// 発射後の戻りを待つステート。発射クリップの ReleaseEnd で終わる。
    /// 終わった時点で subAction が押しっぱなしなら次の矢を引く（連射）。
    /// </summary>
    public class BowStateRelease : BowState
    {
        private float _enterTime;
        private bool _hasEntered;
        private bool _isReleaseEnded;

        public BowStateRelease(BowController context)
            : base(context) { }

        public override void Enter()
        {
            // 戻りの最中に攻撃入力が残っていると、次の構えで暴発するため消費する
            bow.Input.ConsumeAttack();
            _enterTime = Time.time;
            _hasEntered = false;
            _isReleaseEnded = false;
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            if (
                type == WeaponAnimationEventType.ReleaseEnd
                && sourceState.shortNameHash == PlayerAnimatorStates.Fire
            )
                _isReleaseEnded = true;
        }

        public override void Update()
        {
            // 戻りの途中で構えボタンを離したら、照準・カメラは先に解除する
            if (!bow.Input.subAction)
                bow.Player.IsAiming = false;

            if (_isReleaseEnded)
            {
                Finish();
                return;
            }

            if (bow.TryGetUpperBodyState(PlayerAnimatorStates.Fire, out _))
            {
                _hasEntered = true;
            }
            else if (_hasEntered)
            {
                Debug.LogWarning(
                    $"[{bow.name}] 発射クリップの ReleaseEnd イベントを受信しないまま Fire ステートを抜けました。"
                        + "発射クリップに ReleaseEnd を追加してください。"
                );
                Finish();
            }
            else if (Time.time - _enterTime > EnterStateTimeout)
            {
                Debug.LogWarning(
                    $"[{bow.name}] UpperBody の Fire ステートへ遷移できませんでした。Animator の遷移設定を確認してください。"
                );
                bow.Animator.ResetTrigger(PlayerAnimatorParams.Fire);
                Finish();
            }
        }

        private void Finish()
        {
            if (bow.Input.subAction)
                bow.ChangeState(new BowStateAim(bow));
            else
                bow.ChangeState(new BowStateFree(bow));
        }
    }
}
