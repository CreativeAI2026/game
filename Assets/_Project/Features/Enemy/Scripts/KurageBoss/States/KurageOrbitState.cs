using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃の合間にステージの周りを回る。多くはプレイヤーの方へ回り込み、時々逆へ回る。
    /// 決めた時間回ったら次の攻撃を選ぶ。
    /// </summary>
    public class KurageOrbitState : KurageBaseState
    {
        private float _duration;
        private float _direction;
        private float _timer;

        public KurageOrbitState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            _duration = Random.Range(boss.OrbitDurationRange.x, boss.OrbitDurationRange.y);

            float towardPlayer = boss.Mover.DirectionToward(boss.Player.transform.position, 0f);
            if (Mathf.Approximately(towardPlayer, 0f))
                towardPlayer = 1f;
            _direction = Random.value < boss.OrbitTowardPlayerChance ? towardPlayer : -towardPlayer;
        }

        public override void Update()
        {
            boss.Mover.Move(_direction, boss.OrbitSpeed);
            FaceStage();

            _timer += Time.deltaTime;
            if (_timer >= _duration)
                boss.StartNextAttack();
        }
    }
}
