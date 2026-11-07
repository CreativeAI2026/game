using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// わっか投げ。リングのクリップで頭上のわっかを掴み(RingGrab)、プレイヤーの今いる場所へ投げ上げる(RingThrow)。
    /// 投げた後のわっかは <see cref="KurageHaloRing"/> が自分で動かすので、ボスはクリップが終われば次の行動へ移る。
    /// 捕まえた時の待機への切り替えは <see cref="KurageBossController"/> が行う。
    /// </summary>
    public class KurageRingThrowState : KurageBaseState
    {
        // 落下地点をステージの縁から内側へ離す距離（m）。縁ぎりぎりに落として避け場所が無くならないように
        private const float LandingEdgeInset = 1f;

        private bool _thrown;

        public KurageRingThrowState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _thrown = false;
            boss.Animator.SetTrigger(KurageAnimatorParams.Ring);
        }

        public override void Update()
        {
            boss.Mover.Move(0f, 0f);
            FaceStage();

            if (IsAnimationFinished(KurageAnimatorParams.Ring))
                boss.FinishAttack();
        }

        public override void OnAnimationEvent(KurageAnimationEvent animationEvent)
        {
            switch (animationEvent)
            {
                case KurageAnimationEvent.RingGrab:
                    boss.Halo.Grab();
                    break;
                case KurageAnimationEvent.RingThrow:
                    if (!_thrown)
                        Throw();
                    break;
            }
        }

        public override void Exit()
        {
            // 投げる前に中断された(怯み等)ら、掴んでいたわっかを頭上へ戻す。投げた後のわっかはそのまま飛ばす
            if (!_thrown)
                boss.Halo.ReturnToHead();
        }

        private void Throw()
        {
            _thrown = true;
            Bounds stage = boss.Mover.Path.StageBounds;
            Vector3 landing = boss.Player.transform.position;
            landing.x = Mathf.Clamp(
                landing.x,
                stage.min.x + LandingEdgeInset,
                stage.max.x - LandingEdgeInset
            );
            landing.z = Mathf.Clamp(
                landing.z,
                stage.min.z + LandingEdgeInset,
                stage.max.z - LandingEdgeInset
            );
            landing.y = boss.Mover.Path.StageTopY;
            boss.Halo.Throw(landing, boss.Player.transform);
        }
    }
}
