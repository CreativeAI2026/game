using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 腕の鎌を構えて、正面からの攻撃を受け止めるステート。その場に止まり、プレイヤーの方を向き続ける。
    /// 受け止める判定そのものは <see cref="TBossGuard"/> が行い、ここは構えの時間と、終わった後の行動を決める。
    /// 防御中に連打されたら、コントローラーがはじき返し（<see cref="TBossRepelState"/>）へ切り替える。
    /// </summary>
    public sealed class TBossGuardState : TBossBaseState
    {
        private static readonly int IsGuardingHash = Animator.StringToHash("IsGuarding");

        // 構えたままプレイヤーへ向き直る補間速度。様子見（8）より遅くし、素早く横へ回り込めば背後を取れるようにする
        private const float RotateSpeed = 4f;

        private readonly float _duration;
        private readonly bool _attackWhenDone;
        private float _timer;

        /// <param name="duration">構えを続ける時間（秒）</param>
        /// <param name="attackWhenDone">true なら構えが終わったら攻撃に移る（読みの防御）。false なら様子見へ戻る</param>
        public TBossGuardState(TutorialBossController controller, float duration, bool attackWhenDone)
            : base(controller)
        {
            _duration = duration;
            _attackWhenDone = attackWhenDone;
        }

        public override void Enter()
        {
            DevLog.Log($"[TutorialBoss] 防御開始（{_duration:0.0}秒、終わったら{(_attackWhenDone ? "攻撃" : "様子見")}）");

            _timer = 0f;
            boss.Guard.IsGuarding = true;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
                boss.SetAgentStopped(true);
                boss.Agent.updateRotation = false;
            }

            boss.Animator.SetBool(IsGuardingHash, true);
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            FacePlayer();

            _timer += Time.deltaTime;
            if (_timer < _duration)
            {
                return;
            }

            if (_attackWhenDone)
            {
                boss.TransitionToAttack();
            }
            else
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        public override void Exit()
        {
            boss.Guard.IsGuarding = false;
            boss.Animator.SetBool(IsGuardingHash, false);

            if (boss.Agent != null)
            {
                if (boss.Agent.isOnNavMesh)
                {
                    boss.SetAgentStopped(false);
                }
                boss.Agent.updateRotation = true;
            }
        }

        private void FacePlayer()
        {
            Vector3 toPlayer = boss.Player.transform.position - boss.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(toPlayer),
                Time.deltaTime * RotateSpeed
            );
        }
    }
}
