using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 怯み。決めた時間その場で止まり、周回に戻る。
    /// 怯みのクリップは後から入る。Animator の Flinch ステートにクリップを入れれば、ここは変えずに再生される。
    /// </summary>
    public class KurageFlinchState : KurageBaseState
    {
        private float _timer;

        public KurageFlinchState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            boss.Animator.SetTrigger(KurageAnimatorParams.Flinch);
        }

        public override void Update()
        {
            boss.Mover.Move(0f, 0f);
            _timer += Time.deltaTime;
            if (_timer >= boss.FlinchDuration)
                boss.ChangeState(new KurageOrbitState(boss));
        }
    }
}
