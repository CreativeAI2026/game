using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// NavMeshAgent が目的地へ向かって進めているかを監視する。
    /// 経路が引けない・到達不能な地点の手前で止まった・何かに引っかかって進まない、をまとめて「詰まり」として検出する。
    /// 到着判定は各ステートが持つため、ここでは「移動したいのに進んでいない」ことだけを見る。
    /// </summary>
    public class TBossStuckDetector
    {
        // 移動量を確認する間隔（秒）
        private const float CheckInterval = 1f;

        // 確認間隔の間に、本来進めるはずの距離（速度×時間）のこの割合すら進めていなければ詰まりとみなす。
        // 速度に対する割合で持つことで、歩き・走りやボスのスケールに関係なく同じ基準で判定できる。
        private const float MinProgressRatio = 0.2f;

        private readonly NavMeshAgent _agent;
        private Vector3 _lastPosition;
        private float _timer;

        public TBossStuckDetector(NavMeshAgent agent)
        {
            _agent = agent;
            Reset();
        }

        /// <summary>計測をやり直す。目的地を大きく変えた・意図的に停止させた直後に呼ぶ。</summary>
        public void Reset()
        {
            _timer = 0f;
            _lastPosition = _agent.transform.position;
        }

        /// <summary>移動させたいフレームに毎回呼ぶ。詰まりを検出した場合に true を返す。</summary>
        public bool Tick(float deltaTime)
        {
            if (!_agent.isOnNavMesh || _agent.pathPending || _agent.isStopped)
            {
                Reset();
                return false;
            }

            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                return true;
            }

            _timer += deltaTime;
            if (_timer < CheckInterval)
            {
                return false;
            }

            Vector3 moved = _agent.transform.position - _lastPosition;
            moved.y = 0f;
            float expected = _agent.speed * _timer;
            Reset();

            return moved.magnitude < expected * MinProgressRatio;
        }
    }
}
