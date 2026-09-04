using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤー方向に一直線で突進する遠距離攻撃ステート。
    /// フェーズ1（予備動作）：プレイヤー方向にホーミング回転しながら待機。
    /// フェーズ2（突進）：開始時の向きを固定して直線移動。Obstacleタグの壁にぶつかると終了。
    ///
    /// ※予備動作のアニメーション導入時は Enter 内の
    ///   TODO コメント箇所に Animator.SetTrigger("DashWindup") を追加する。
    /// </summary>
    public class MidBossDashAttackState : MidBossBaseState
    {
        private enum Phase
        {
            Windup,
            Dash,
        }

        private Phase _phase;
        private float _phaseTimer;

        // 突進開始時に固定する方向
        private Vector3 _dashDir;

        // 突進中のSphereCast半径
        private const float DashCastRadius = 0.5f;

        public MidBossDashAttackState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 遠距離攻撃1（突進）ステート開始");
            _phase = Phase.Windup;
            _phaseTimer = 0f;

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }

            // TODO: アニメーション導入時はここで Animator.SetTrigger("DashWindup") を追加
        }

        public override void Update()
        {
            _phaseTimer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Windup:
                    UpdateWindup();
                    break;
                case Phase.Dash:
                    UpdateDash();
                    break;
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
                con.Agent.ResetPath();
            }
            // TODO: アニメーション導入時はここで IsRunning フラグ等をリセット
        }

        private void UpdateWindup()
        {
            // 予備動作中はプレイヤー方向にホーミング回転
            if (con.Player != null)
            {
                Vector3 dir = con.Player.transform.position - con.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion target = Quaternion.LookRotation(dir.normalized);
                    con.transform.rotation = Quaternion.Slerp(
                        con.transform.rotation,
                        target,
                        Time.deltaTime * 8f
                    );
                }
            }

            if (_phaseTimer >= con.DashWindupDuration)
            {
                StartDash();
            }
        }

        private void StartDash()
        {
            _phase = Phase.Dash;
            _phaseTimer = 0f;

            // この時点での forward を固定（突進中は角度を一切変えない）
            _dashDir = con.transform.forward;
            _dashDir.y = 0f;
            if (_dashDir.sqrMagnitude < 0.001f)
            {
                _dashDir = Vector3.forward;
            }
            _dashDir.Normalize();

            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }

            // TODO: アニメーション導入時はここで Animator.SetTrigger("Dash") を追加
            Debug.Log("[MidBoss] 突進開始");
        }

        private void UpdateDash()
        {
            // タイムアウト
            if (_phaseTimer >= con.DashMaxDuration)
            {
                Debug.Log("[MidBoss] 突進タイムアウト");
                EndDash();
                return;
            }

            // 壁（Obstacle）との衝突チェック
            Vector3 origin = con.transform.position + Vector3.up * 0.5f;
            if (
                Physics.SphereCast(
                    origin,
                    DashCastRadius,
                    _dashDir,
                    out RaycastHit hit,
                    0.5f,
                    con.ObstacleLayer
                )
            )
            {
                if (hit.collider.CompareTag("Obstacle"))
                {
                    Debug.Log("[MidBoss] 壁にぶつかった。突進終了");
                    EndDash();
                    return;
                }
            }

            // 直線突進（NavMeshAgentのMoveで移動）
            if (con.Agent != null)
            {
                con.Agent.Move(_dashDir * con.DashSpeed * Time.deltaTime);
            }
        }

        private void EndDash()
        {
            con.ChangeState(
                new MidBossWaitState(con, con.WaitAfterAttackDuration, new MidBossStartState(con))
            );
        }
    }
}
