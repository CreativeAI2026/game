using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃の合間にステージの周りを回る。多くはプレイヤーの方へ回り込み、時々逆へ回る。
    /// 決めた時間回ったら次の攻撃を選ぶ。
    ///
    /// 周回に入った時に、この周回中にスピードアップ・切り返しをするかを1回ずつ抽選し、
    /// 当たったら周回中のランダムな時刻に起こす。毎秒抽選すると周回が長いほど起きやすくなり、
    /// 設定した確率と実際の頻度がずれるため、周回ごとの確率にしている。
    /// </summary>
    public class KurageOrbitState : KurageBaseState
    {
        /// <summary>タイマーが到達しない時刻。抽選に外れて起こさないことを表す。</summary>
        private const float NotScheduled = float.PositiveInfinity;

        // 切り返す時刻を選ぶ範囲(周回時間に対する割合)。始まった直後や終わる直前だとほとんど動かずに攻撃へ移り、
        // 切り返しとして見えないため、両端を除く
        private const float TurnWindowStart = 0.2f;
        private const float TurnWindowEnd = 0.7f;

        private float _duration;
        private float _direction;
        private float _timer;
        private float _speedUpStartTime;
        private float _speedUpEndTime;
        private float _turnTime;

        // 向きの付いた今の速さ（m/秒）。符号は Move の direction と同じ(+ で周回路の s が増える向き)
        private float _velocity;

        public KurageOrbitState(KurageBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            _velocity = 0f;
            _duration = Random.Range(boss.OrbitDurationRange.x, boss.OrbitDurationRange.y);

            float towardPlayer = boss.Mover.DirectionToward(boss.Player.transform.position, 0f);
            if (Mathf.Approximately(towardPlayer, 0f))
                towardPlayer = 1f;
            _direction = Random.value < boss.OrbitTowardPlayerChance ? towardPlayer : -towardPlayer;

            ScheduleSpeedUp();
            _turnTime =
                Random.value < boss.TurnProbability
                    ? Random.Range(_duration * TurnWindowStart, _duration * TurnWindowEnd)
                    : NotScheduled;
        }

        public override void Update()
        {
            if (_timer >= _turnTime)
            {
                _direction = -_direction;
                // 1回の周回で切り返すのは1回だけ
                _turnTime = NotScheduled;
            }

            bool isSpeedUp = _timer >= _speedUpStartTime && _timer < _speedUpEndTime;
            float targetSpeed = isSpeedUp ? boss.HighOrbitSpeed : boss.OrbitSpeed;
            // Move は渡した速さをそのまま使うので、ここで少しずつ変える。
            // 切り返しは目標の向きを反転するだけで、減速→止まる→逆へ加速になる
            _velocity = Mathf.MoveTowards(
                _velocity,
                _direction * targetSpeed,
                boss.OrbitAcceleration * Time.deltaTime
            );
            boss.Mover.Move(Mathf.Sign(_velocity), Mathf.Abs(_velocity));
            FaceStage();

            _timer += Time.deltaTime;
            // 自動の攻撃を止めている間(デバッグで1つずつ確かめる時)は回り続ける
            if (_timer >= _duration && boss.AutoAttacks)
                boss.StartNextAttack();
        }

        /// <summary>
        /// スピードアップを抽選し、当たったら始まりと終わりの時刻を決める。
        /// 途中で周回が終わって実質短くならないよう、周回時間内に収まる範囲から始まりを選ぶ。
        /// </summary>
        private void ScheduleSpeedUp()
        {
            if (Random.value >= boss.SpeedUpProbability)
            {
                _speedUpStartTime = NotScheduled;
                _speedUpEndTime = NotScheduled;
                return;
            }

            float speedUpDuration = _duration * boss.SpeedUpDurationRatio;
            _speedUpStartTime = Random.Range(0f, _duration - speedUpDuration);
            _speedUpEndTime = _speedUpStartTime + speedUpDuration;
        }
    }
}
