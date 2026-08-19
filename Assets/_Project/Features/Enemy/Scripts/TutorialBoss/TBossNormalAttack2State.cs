using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃用の腕を横に振り、ヒット時にプレイヤーを引き寄せる攻撃ステート。
    /// 専用アニメーションが未実装のため、タイマーで進行管理する。
    /// 引き寄せはプレイヤーのRigidbodyに力を加える方式で実装する。
    /// </summary>
    public class TBossNormalAttack2State : TBossBaseState
    {
        private float _timer;

        // アニメーション未実装のため、タイマーで疑似的にフェーズを管理
        private const float WindUpDuration = 0.4f; // 予備動作
        private const float ActiveDuration = 0.4f; // 判定発生
        private const float RecoveryDuration = 0.6f; // 硬直

        private bool _hitChecked;
        private bool _playerPulled;

        // 引き寄せる距離（ボスの前方 attackRange 分）
        private const float PullDistance = 1.5f;

        // 引き寄せの力
        private const float PullForce = 15f;

        public TBossNormalAttack2State(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 通常攻撃2ステート開始");

            _timer = 0f;
            _hitChecked = false;
            _playerPulled = false;

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            // アニメーションが実装されたら "Attack2" Triggerを使用する
            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Attack2");
            }

            // プレイヤー方向へ向く
            if (boss.Player != null)
            {
                Vector3 dir = (boss.Player.transform.position - boss.transform.position).normalized;
                dir.y = 0f;
                if (dir != Vector3.zero)
                {
                    boss.transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            float totalDuration = WindUpDuration + ActiveDuration + RecoveryDuration;

            // 判定フェーズ
            if (
                _timer >= WindUpDuration
                && _timer < WindUpDuration + ActiveDuration
                && !_hitChecked
            )
            {
                _hitChecked = true;
                CheckHitAndPull();
            }

            if (_timer >= totalDuration)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        public override void Exit()
        {
            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }
        }

        private void CheckHitAndPull()
        {
            if (boss.Player == null || _playerPulled)
            {
                return;
            }

            float dist = Vector3.Distance(boss.transform.position, boss.Player.transform.position);

            if (dist > boss.NormalAttack2Range)
            {
                return;
            }

            // 横方向の判定（前方±90°以内）
            Vector3 dirToPlayer = (
                boss.Player.transform.position - boss.transform.position
            ).normalized;
            float angle = Vector3.Angle(boss.transform.right, dirToPlayer);
            // 左右どちらかの腕の振り（前方180°）
            if (Vector3.Angle(boss.transform.forward, dirToPlayer) > 90f)
            {
                return;
            }

            // 引き寄せ（Rigidbodyに力を加える）
            Rigidbody playerRb = boss.Player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                _playerPulled = true;
                Vector3 pullDir = (
                    boss.transform.position - boss.Player.transform.position
                ).normalized;
                // Y方向は制限して横引き寄せに近づける
                pullDir.y = 0f;
                pullDir = pullDir.normalized;

                // 既存の速度をリセットして引き寄せ力を加える
                playerRb.linearVelocity = Vector3.zero;
                playerRb.AddForce(pullDir * PullForce, ForceMode.Impulse);

                Debug.Log("[TutorialBoss] 通常攻撃2 ヒット：プレイヤーを引き寄せました");
            }
        }
    }
}
