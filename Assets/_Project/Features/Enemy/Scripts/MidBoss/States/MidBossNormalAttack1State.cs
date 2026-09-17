using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 腕オブジェクトを使った近接たたきつけ攻撃ステート。
    /// フェーズ1：振りかぶり（armWindupEuler 方向へゆっくり回転）
    /// フェーズ2：たたきつけ（armStrikeEuler 方向へ高速回転 + OverlapSphere で当たり判定）
    ///
    /// ※アニメーション未導入のため armTransform を直接 Transform 操作で表現する。
    ///   アニメーション導入時は UpdateArmRotation() 内のコードを削除し、
    ///   Animator.SetTrigger("NormalAttack1") に差し替えるだけで移行できる。
    /// </summary>
    public class MidBossNormalAttack1State : MidBossBaseState
    {
        private enum Phase
        {
            Windup,
            Strike,
            Done,
        }

        private Phase _phase;
        private float _phaseTimer;
        private bool _hitChecked;

        // 振りかぶり開始時のローカル回転（Exit で戻すために記録）
        private Quaternion _originalArmRotation;

        public MidBossNormalAttack1State(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 通常攻撃1ステート開始");
            _phase = Phase.Windup;
            _phaseTimer = 0f;
            _hitChecked = false;

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }

            // プレイヤー方向を向く
            if (con.Player != null)
            {
                Vector3 dir = (con.Player.transform.position - con.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    con.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            // 現在の腕の回転を記録
            if (con.ArmTransform != null)
            {
                _originalArmRotation = con.ArmTransform.localRotation;
            }

            // TODO: アニメーション導入時はここで Animator.SetTrigger("NormalAttack1") を追加し、
            //       以下の UpdateArmRotation() の呼び出しは削除する
        }

        public override void Update()
        {
            _phaseTimer += UnityEngine.Time.deltaTime;

            switch (_phase)
            {
                case Phase.Windup:
                    UpdateArmRotation(con.ArmWindupEuler, con.NormalAttack1WindupDuration);
                    if (_phaseTimer >= con.NormalAttack1WindupDuration)
                    {
                        _phase = Phase.Strike;
                        _phaseTimer = 0f;
                        // TODO: アニメーション導入時はたたきつけアニメーションのトリガーをここで発火
                    }
                    break;

                case Phase.Strike:
                    UpdateArmRotation(con.ArmStrikeEuler, con.NormalAttack1StrikeDuration);

                    // たたきつけ開始直後（最初のフレーム）に当たり判定
                    if (!_hitChecked)
                    {
                        _hitChecked = true;
                        CheckHit();
                    }

                    if (_phaseTimer >= con.NormalAttack1StrikeDuration)
                    {
                        _phase = Phase.Done;
                    }
                    break;

                case Phase.Done:
                    con.ChangeState(
                        new MidBossWaitState(
                            con,
                            con.WaitAfterAttackDuration,
                            new MidBossStartState(con)
                        )
                    );
                    break;
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }
            // 腕の回転を元に戻す（アニメーション導入後は削除）
            if (con.ArmTransform != null)
            {
                con.ArmTransform.localRotation = _originalArmRotation;
            }
        }

        /// <summary>
        /// 腕オブジェクトをターゲット回転に向けて補間する。
        /// ※アニメーション導入時はこのメソッド全体を削除し、Animator に委譲する。
        /// </summary>
        private void UpdateArmRotation(Vector3 targetEuler, float duration)
        {
            if (con.ArmTransform == null)
                return;

            Quaternion target = Quaternion.Euler(targetEuler);
            float speed = 1f / Mathf.Max(duration, 0.001f);
            con.ArmTransform.localRotation = Quaternion.RotateTowards(
                con.ArmTransform.localRotation,
                target,
                speed * 180f * UnityEngine.Time.deltaTime
            );
        }

        /// <summary>
        /// 腕先端でのOverlapSphereによる当たり判定。
        /// </summary>
        private void CheckHit()
        {
            if (con.ArmTransform == null)
                return;

            Collider[] hits = Physics.OverlapSphere(con.ArmTransform.position, con.ArmHitRadius);

            foreach (var col in hits)
            {
                if (col.CompareTag("Player"))
                {
                    var damageable = col.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(con.ArmAttackDamage, false);
                        Debug.Log("[MidBoss] 通常攻撃1 ヒット！");
                    }
                    break;
                }
            }
        }
    }
}
