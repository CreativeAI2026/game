using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓のステートマシンの基底クラス。
    /// StateFree → StateAim → StateFire → StateRelease のサイクルで遷移する。
    /// </summary>
    public abstract class BowState
    {
        protected BowController ctx;

        public BowState(BowController context)
        {
            ctx = context;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }
    }

    /// <summary>
    /// 弓の待機ステート。エイムしていない通常状態。
    /// </summary>
    public class StateFree : BowState
    {
        public StateFree(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx._playerController.IsAiming = false;
            ctx._playerController.CanChangeWeapon = true;
            ctx._drawProgress = 0f;
            ctx.DestroyArrow();
            ctx.HideCrossHair();
        }

        public override void Update()
        {
            if (ctx._input.subAction)
            {
                ctx.ChangeState(new StateAim(ctx));
            }

            // 待機中に攻撃入力が残っていると、次にAimに入った瞬間に暴発するため消費しておく
            if (ctx._input.ConsumeAttack()) { }
        }
    }

    /// <summary>
    /// 弓のエイムステート。弦を引き絞り、射撃入力を待つ状態。
    /// </summary>
    public class StateAim : BowState
    {
        public StateAim(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx._playerController.IsAiming = true;
            ctx._playerController.CanChangeWeapon = false;
            ctx._isArrowAtNock = false;
            ctx.SpawnArrowInHand();
            ctx.ShowCrossHair();
        }

        public override void Update()
        {
            if (!ctx._input.subAction)
            {
                ctx.ChangeState(new StateRelease(ctx));
                return;
            }

            if (ctx._drawProgress < 1f)
            {
                ctx._drawProgress += Time.deltaTime / ctx._drawDuration;
                if (ctx._drawProgress >= 1f)
                {
                    ctx._drawProgress = 1f;
                }
            }

            if (ctx._drawProgress >= 1f && !ctx._isArrowAtNock)
            {
                ctx.MoveArrowToNock();
                ctx.ASource.pitch = Random.Range(0.8f, 1.2f);
                ctx.ASource.PlayOneShot(ctx.DrawSound);
            }

            if (ctx._input.ConsumeAttack())
            {
                if (ctx._isArrowAtNock)
                {
                    ctx.ChangeState(new StateFire(ctx));
                }
            }
        }

        public override void Exit()
        {
            // IsAiming = false はここでは行わない。
            // StateFire（発射中）→ StateRelease（弦戻し中）はまだエイム継続のため。
            // IsAiming は StateFree.Enter() で確実に false にリセットされる。
            ctx.HideCrossHair();
        }
    }

    /// <summary>
    /// 弓の発射ステート。即座にFireArrowを呼び、StateReleaseへ遷移する一瞬のステート。
    /// </summary>
    public class StateFire : BowState
    {
        public StateFire(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.FireArrow();
            ctx.ChangeState(new StateRelease(ctx));

            ctx.ASource.pitch = Random.Range(0.8f, 1.2f);
            ctx.ASource.PlayOneShot(ctx.ShootSound);
        }
    }

    /// <summary>
    /// 弦の戻りアニメーションを管理するステート。
    /// 戻りきった後、subActionが押しっぱなしなら次の矢を引く（連射対応）。
    /// </summary>
    public class StateRelease : BowState
    {
        public StateRelease(BowController context)
            : base(context) { }

        public override void Enter()
        {
            // キャンセル（エイム解除）でここに来た場合、矢が手元に残っているため消す。
            // Fire経由なら矢は既に飛んでおりnullになっているので安全
            ctx.DestroyArrow();
            if (ctx._input.ConsumeAttack()) { } // 弦の戻し中に攻撃入力が残っていると暴発するため消費

            if (ctx._input.subAction)
            {
                ctx._playerController.IsAiming = true;
                ctx.ShowCrossHair();
            }
        }

        public override void Update()
        {
            if (!ctx._input.subAction && ctx._playerController.IsAiming)
            {
                ctx._playerController.IsAiming = false;
                ctx.HideCrossHair();
            }

            ctx._drawProgress -= Time.deltaTime / ctx._releaseDuration;
            if (ctx._drawProgress <= 0f)
            {
                ctx._drawProgress = 0f;

                // 戻りきった時点でsubActionを押しっぱなしなら、次の矢を即座に引き始める（連射）
                if (ctx._input.subAction)
                {
                    ctx.ChangeState(new StateAim(ctx));
                }
                else
                {
                    ctx.ChangeState(new StateFree(ctx));
                }
            }
        }
    }
}
