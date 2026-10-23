using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 近接攻撃（通常攻撃・鎌）の前に、振れる間合いまで走り込む処理。
    /// 様子見の間合いの外から直接仕掛けられるようにするためのもので、各攻撃ステートが所有して使う。
    /// </summary>
    public class TBossMeleeApproach
    {
        public enum Result
        {
            /// <summary>走り込み中。</summary>
            Running,

            /// <summary>間合いに入り、振りが障害物に遮られない。振ってよい。</summary>
            Arrived,

            /// <summary>到達できない位置にいて近づけない（段差の上・NavMesh外など）。</summary>
            Unreachable,

            /// <summary>逃げ続けられて時間内に追いつけなかった。</summary>
            TimedOut,
        }

        // 走り込みを打ち切る時間。逃げ続けるプレイヤーを攻撃ステートのまま延々追い回さないため
        private const float Timeout = 5f;

        private readonly TutorialBossController _boss;
        private readonly float _strikeRange;
        private TBossStuckDetector _stuckDetector;
        private float _timer;

        /// <param name="strikeRange">この距離以内で射線が通れば振る。</param>
        public TBossMeleeApproach(TutorialBossController boss, float strikeRange)
        {
            _boss = boss;
            _strikeRange = strikeRange;
        }

        /// <summary>
        /// 間合いに入っていて、振りが障害物に遮られないか。
        /// 直線距離だけで振ると、薄い壁や柱の向こうのプレイヤーへ空振りするため射線も確かめる。
        /// </summary>
        public bool CanStrikeNow()
        {
            return Vector3.Distance(_boss.transform.position, _boss.Player.transform.position)
                    <= _strikeRange
                && _boss.AttackFeasibility.HasMeleeLine();
        }

        public void Begin()
        {
            _timer = 0f;
            if (_boss.Agent != null)
            {
                _boss.Agent.speed = _boss.RunSpeed;
                _boss.SetAgentStopped(false);
                _stuckDetector = new TBossStuckDetector(_boss.Agent);
            }
        }

        public Result Tick(float deltaTime)
        {
            if (_boss.Agent != null)
            {
                _boss.Agent.SetDestination(_boss.Player.transform.position);
            }

            if (CanStrikeNow())
            {
                return Result.Arrived;
            }

            if (_stuckDetector != null && _stuckDetector.Tick(deltaTime))
            {
                return Result.Unreachable;
            }

            _timer += deltaTime;
            return _timer >= Timeout ? Result.TimedOut : Result.Running;
        }
    }
}
