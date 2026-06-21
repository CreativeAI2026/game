using UnityEngine;

namespace CreativeAI.Gameplay
{
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

    // ① 待機状態
    public class StateFree : BowState
    {
        public StateFree(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx._playerController.IsAiming = false;
            ctx._playerController.CanChangeWeapon = true; // 武器切り替えOK
            ctx._drawProgress = 0f;
            ctx.DestroyArrow();
            ctx.HideCrossHair();
        }

        public override void Update()
        {
            // エイム（右クリック）が押されたら構えステートへ
            if (ctx._input.subAction)
            {
                ctx.ChangeState(new StateAim(ctx));
            }

            // 待機中に空打ち入力が残らないよう、念のためここで消費しておく
            if (ctx._input.ConsumeAttack()) { }
        }
    }

    // ② 構え・引き絞り状態
    public class StateAim : BowState
    {
        public StateAim(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx._playerController.IsAiming = true;
            ctx._playerController.CanChangeWeapon = false; // 武器切り替え禁止
            ctx._isArrowAtNock = false;
            ctx.SpawnArrowInHand();
            ctx.ShowCrossHair();
        }

        public override void Update()
        {
            // エイムボタンを離したら、発射キャンセル（弦を戻すステートへ）
            if (!ctx._input.subAction)
            {
                ctx.ChangeState(new StateRelease(ctx));
                return;
            }

            // 弦を引き絞る
            if (ctx._drawProgress < 1f)
            {
                ctx._drawProgress += Time.deltaTime / ctx._drawDuration;
                if (ctx._drawProgress >= 1f)
                {
                    ctx._drawProgress = 1f;
                }
            }

            // 引き切り完了時に矢をノック位置へセット
            if (ctx._drawProgress >= 1f && !ctx._isArrowAtNock)
            {
                ctx.MoveArrowToNock();
            }

            // 攻撃入力（左クリック）が消費された場合
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

    // ③ 発射状態（一瞬で終わる）
    public class StateFire : BowState
    {
        public StateFire(BowController context)
            : base(context) { }

        public override void Enter()
        {
            ctx.FireArrow();
            // 発射後、すぐに弦を戻すステートへ移行
            ctx.ChangeState(new StateRelease(ctx));
        }
    }

    // ④ 弦の戻し・発射後待機状態
    public class StateRelease : BowState
    {
        public StateRelease(BowController context)
            : base(context) { }

        public override void Enter()
        {
            // キャンセル（エイム解除）でここに来た場合は矢が手元に残っているので消す
            // Fire後なら矢はすでに飛んでいっているので null になっており安全
            ctx.DestroyArrow();
            if (ctx._input.ConsumeAttack()) { } // 戻し中の誤爆防止

            // subAction（右クリック）を継続中はクロスヘアーとIsAimingを維持する
            // → subActionを離した場合はStateFreeに遷移する際にリセットされる
            if (ctx._input.subAction)
            {
                ctx._playerController.IsAiming = true;
                ctx.ShowCrossHair();
            }
        }

        public override void Update()
        {
            // 右クリックを離した瞬間、エイム状態を解除する（画面酔い防止）
            if (!ctx._input.subAction && ctx._playerController.IsAiming)
            {
                ctx._playerController.IsAiming = false;
                ctx.HideCrossHair();
            }

            // 弦がスッと戻っていく
            ctx._drawProgress -= Time.deltaTime / ctx._releaseDuration;
            if (ctx._drawProgress <= 0f)
            {
                ctx._drawProgress = 0f;

                // 戻りきった時、まだ右クリックを押しっぱなしなら次の矢を引く
                if (ctx._input.subAction)
                {
                    ctx.ChangeState(new StateAim(ctx));
                }
                else
                {
                    // 離していれば完全な待機状態へ
                    ctx.ChangeState(new StateFree(ctx));
                }
            }
        }
    }
}
