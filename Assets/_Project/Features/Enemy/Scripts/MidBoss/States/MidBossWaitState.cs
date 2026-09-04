using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 汎用待機ステート。一定時間何もしない後、指定したステートへ遷移する。
    /// コンストラクタで duration と nextState を受け取ることで、
    /// どの攻撃ステートからも柔軟に使用できる。
    /// </summary>
    public class MidBossWaitState : MidBossBaseState
    {
        private readonly float _duration;
        private readonly IEnemyState _nextState;
        private float _timer;

        /// <param name="controller">コントローラ参照</param>
        /// <param name="duration">待機時間（秒）</param>
        /// <param name="nextState">待機完了後に遷移するステート</param>
        public MidBossWaitState(MidBossController controller, float duration, IEnemyState nextState)
            : base(controller)
        {
            _duration = duration;
            _nextState = nextState;
        }

        public override void Enter()
        {
            Debug.Log($"[MidBoss] 待機ステート開始（{_duration}秒）");
            _timer = 0f;

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }
        }

        public override void Update()
        {
            _timer += UnityEngine.Time.deltaTime;
            if (_timer >= _duration)
            {
                con.ChangeState(_nextState);
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }
        }
    }
}
