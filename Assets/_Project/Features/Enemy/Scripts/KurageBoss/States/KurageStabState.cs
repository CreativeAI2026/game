using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 触手の突き。周回路に沿ってプレイヤーを一定時間追いかけ、立ち止まった瞬間のプレイヤーの位置を狙いに決める。
    /// 溜めの間に体ごとその場所へ向け、一気に針を伸ばす。狙いを決めた後はプレイヤーを追わないので、動けば避けられる。
    /// 当たり判定と伸ばす長さは、クリップのアニメーションイベント(StabHitStart / StabHitEnd)に合わせて入れ切りする。
    /// </summary>
    public class KurageStabState : KurageBaseState
    {
        private enum Phase
        {
            Chase,
            Windup,
            Attack,
        }

        // プレイヤーの真横からこの距離（m）以内に来たら追いかけるのをやめて待つ
        private const float ChaseArriveDistance = 1f;

        private Phase _phase;
        private float _timer;
        private bool _hitStarted;
        private bool _hitEnded;
        private Vector3 _target;

        public KurageStabState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _phase = Phase.Chase;
            _timer = 0f;
            _hitStarted = false;
            _hitEnded = false;
        }

        public override void Update()
        {
            _timer += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Chase:
                    float direction = boss.Mover.DirectionToward(
                        boss.Player.transform.position,
                        ChaseArriveDistance
                    );
                    boss.Mover.Move(direction, boss.StabChaseSpeed);
                    FaceStage();
                    if (_timer >= boss.StabChaseDuration)
                    {
                        _target = boss.Player.transform.position;
                        boss.StabReach.AimAt(_target);
                        boss.StabEffects.BeginGather();
                        EnterPhase(Phase.Windup);
                    }
                    break;

                case Phase.Windup:
                    boss.Mover.Move(0f, 0f);
                    boss.Mover.FaceTowards(_target, boss.StabTurnSpeed);
                    if (_timer >= boss.StabWindupDuration)
                    {
                        EnterPhase(Phase.Attack);
                        boss.Animator.SetTrigger(KurageAnimatorParams.Stab);
                    }
                    break;

                case Phase.Attack:
                    UpdateReachWeight();
                    if (IsAnimationFinished(KurageAnimatorParams.Stab))
                        boss.FinishAttack();
                    break;
            }
        }

        public override void OnAnimationEvent(KurageAnimationEvent animationEvent)
        {
            if (_phase != Phase.Attack)
                return;

            switch (animationEvent)
            {
                case KurageAnimationEvent.StabGlint:
                    boss.StabEffects.PlayGlint();
                    break;
                case KurageAnimationEvent.StabHitStart:
                    _hitStarted = true;
                    boss.StabHitbox.EnableHitbox();
                    boss.Sounds.PlayStab();
                    boss.StabEffects.PlayThrust(boss.StabExtendDuration);
                    break;
                case KurageAnimationEvent.StabHitEnd:
                    _hitEnded = true;
                    boss.StabHitbox.DisableHitbox();
                    break;
            }
        }

        public override void Exit()
        {
            boss.StabHitbox.DisableHitbox();
            boss.StabReach.Weight = 0f;
            // 溜めの途中で怯んだ時に、集まる光が出たままにならないようにする
            boss.StabEffects.EndGather();
        }

        private void UpdateReachWeight()
        {
            if (!_hitStarted)
                return;

            float target = _hitEnded ? 0f : 1f;
            float duration = _hitEnded ? boss.StabRetractDuration : boss.StabExtendDuration;
            boss.StabReach.Weight = Mathf.MoveTowards(
                boss.StabReach.Weight,
                target,
                Time.deltaTime / duration
            );
        }

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _timer = 0f;
        }
    }
}
