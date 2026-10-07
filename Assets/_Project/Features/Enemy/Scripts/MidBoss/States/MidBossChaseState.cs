using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 「進む→止まる」を繰り返してプレイヤーをじりじり追う。入場時に追跡ステップ数をランダムに決め、
    /// 近接範囲に入れば攻撃、タイマー切れ時は距離に応じた攻撃ステートへ遷移する。
    /// </summary>
    public class MidBossChaseState : MidBossBaseState
    {
        private int _remainingSteps;
        private float _stepTimer;
        private bool _isMoving;

        public MidBossChaseState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 追跡ステート開始");

            // 追跡タイマーをステップ数でランダムに決定する
            _remainingSteps = Random.Range(con.ChaseStepCountMin, con.ChaseStepCountMax + 1);
            _stepTimer = 0f;
            _isMoving = true;

            if (con.Agent != null)
            {
                con.Agent.speed = con.ChaseSpeed;
                con.Agent.isStopped = false;
            }

            // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", true) を追加
        }

        public override void Update()
        {
            if (con.Player == null)
            {
                con.ChangeState(new MidBossStartState(con));
                return;
            }

            float dist = con.DistanceToPlayer();

            // 追跡中に近接範囲内に入ったら即攻撃へ
            if (dist <= con.AttackRange)
            {
                con.TransitionToMeleeAttack();
                return;
            }

            _stepTimer += UnityEngine.Time.deltaTime;

            if (_isMoving)
            {
                // 移動フェーズ
                if (con.Agent != null)
                {
                    con.Agent.SetDestination(con.Player.transform.position);
                }

                if (_stepTimer >= con.ChaseStepMoveDuration)
                {
                    // 停止フェーズへ切り替え
                    _stepTimer = 0f;
                    _isMoving = false;
                    if (con.Agent != null)
                    {
                        con.Agent.ResetPath();
                    }
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", false) を追加
                }
            }
            else
            {
                // 停止フェーズ
                if (_stepTimer >= con.ChaseStepStopDuration)
                {
                    _stepTimer = 0f;
                    _remainingSteps--;

                    if (_remainingSteps <= 0)
                    {
                        // タイマー切れ：距離に応じて遷移先を選択
                        if (dist <= con.AttackRange)
                        {
                            con.TransitionToMeleeAttack();
                        }
                        else
                        {
                            con.TransitionToRangedAttack();
                        }
                        return;
                    }

                    // 次の移動フェーズへ
                    _isMoving = true;
                    if (con.Agent != null)
                    {
                        con.Agent.isStopped = false;
                    }
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", true) を追加
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("[MidBoss] 追跡ステート終了");
            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.velocity = Vector3.zero;
                con.Agent.isStopped = true;
            }
            // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", false) を追加
        }
    }
}
