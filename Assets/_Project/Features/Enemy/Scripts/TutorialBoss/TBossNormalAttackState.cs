using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 少し踏み込んで攻撃する近接攻撃ステート。
    /// TestEnemyのAttackStateとほぼ同構造。
    /// 当たり判定はAnimationEventで制御する。
    /// </summary>
    public class TBossNormalAttackState : TBossBaseState
    {
        // ホーミング（方向補正）区間
        private const float HomingThreshold = 0.4f;

        // 踏み込み区間
        private const float LungeStartThreshold = 0.4f;
        private const float LungeEndThreshold = 0.7f;

        private const float HomingSpeed = 10f;
        private const float LungeSpeed = 8f;

        // Attackステートに入らないままこの時間を超えたら、トリガー取りこぼしとみなして復帰する
        private const float AttackStateTimeout = 3f;

        private TBossMeleeApproach _approach;
        private bool _isApproaching;
        private float _attackTimer;

        public TBossNormalAttackState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _attackTimer = 0f;
            _approach = new TBossMeleeApproach(boss, boss.AttackRange);

            if (boss.Player != null && !_approach.CanStrikeNow())
            {
                _isApproaching = true;
                _approach.Begin();
            }
            else
            {
                StartAttack();
            }
        }

        private void StartAttack()
        {
            _isApproaching = false;

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Attack");
            }

            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, true);
            }
        }

        public override void Update()
        {
            if (_isApproaching)
            {
                if (boss.Player == null)
                {
                    boss.ChangeState(new TBossPatrolState(boss));
                    return;
                }

                switch (_approach.Tick(Time.deltaTime))
                {
                    case TBossMeleeApproach.Result.Arrived:
                        StartAttack();
                        break;
                    case TBossMeleeApproach.Result.Unreachable:
                        // 到達できない位置にいて近づけない場合は、走り続けず様子見で仕切り直す
                        boss.ChangeState(new TBossWatchState(boss));
                        break;
                    case TBossMeleeApproach.Result.TimedOut:
                        // 逃げ続けるプレイヤーを攻撃ステートのまま延々追い回さないよう打ち切り、追跡へ戻して仕切り直す
                        boss.ChangeState(new TBossChaseState(boss));
                        break;
                }
                return;
            }

            if (boss.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName("Attack"))
            {
                // Attackトリガーの取りこぼしや遷移の中断でAgentを止めたまま無限待機するのを防ぐ
                _attackTimer += Time.deltaTime;
                if (_attackTimer >= AttackStateTimeout)
                {
                    Debug.LogWarning(
                        "[TutorialBoss] Attackステートに入れませんでした。様子見へ復帰します。"
                    );
                    boss.ChangeState(new TBossWatchState(boss));
                }
                return;
            }

            // ホーミング区間：プレイヤー方向へ向き補正
            if (stateInfo.normalizedTime < HomingThreshold && boss.Player != null)
            {
                Vector3 dirToPlayer = (
                    boss.Player.transform.position - boss.transform.position
                ).normalized;
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                    boss.transform.rotation = Quaternion.Slerp(
                        boss.transform.rotation,
                        targetRotation,
                        Time.deltaTime * HomingSpeed
                    );
                }
            }
            // 踏み込み区間
            else if (
                stateInfo.normalizedTime >= LungeStartThreshold
                && stateInfo.normalizedTime <= LungeEndThreshold
            )
            {
                if (boss.Agent != null)
                {
                    boss.Agent.Move(boss.transform.forward * (LungeSpeed * Time.deltaTime));
                }
            }

            // アニメーション終了後の遷移
            if (stateInfo.normalizedTime >= 1.0f)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        public override void Exit()
        {
            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, false);
            }

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }
        }
    }
}
