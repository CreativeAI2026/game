using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 距離を取って遠距離攻撃をしてくる相手へ、腕の鎌で正面を受け止めながらにじり寄るステート。
    /// 構えたまま、すり足で寄るモーション（TBoss_GuardAdvance）を、実際に進んでいる速さに合わせた再生速度で再生する（足が滑らない）。
    /// プレイヤーが攻撃の間合いへ入ってきたら攻撃し、入ってこなければ一定時間待ってから自分から攻撃に入る。
    /// しばらく見えないままなら追跡へ戻る（撃たれている間は位置が分かっているので戻らない）。
    /// </summary>
    public sealed class TBossGuardAdvanceState : TBossBaseState
    {
        // プレイヤーへ向き直る速さ（防御のステートと同じ）
        private const float RotateSpeed = 8f;

        // 見えなくなってから追跡へ戻るまでの猶予（秒）。遠くから撃つ相手は光の外に出やすく、
        // 一瞬見えないだけで戻すと構えた直後に追跡へ戻ってしまう。撃たれるたびに猶予はやり直す（OnPlayerRangedAttack）
        private const float LostSightGraceBeforeChase = 2f;

        private static readonly int IsGuardAdvancingHash = Animator.StringToHash(
            "IsGuardAdvancing"
        );
        private static readonly int GuardAdvanceSpeedHash = Animator.StringToHash(
            "GuardAdvanceSpeed"
        );

        private float _timer;
        private float _duration;

        public TBossGuardAdvanceState(TutorialBossController controller)
            : base(controller) { }

        public override bool AimsHeadAtPlayer => true;
        public override bool CanRepel => true;
        public override bool IsCommitted => true;

        /// <summary>撃たれた・投げられたことで相手の位置は分かるので、見失いの猶予をやり直す。</summary>
        public override void OnPlayerRangedAttack()
        {
            boss.LostSightTimer = 0f;
        }

        public override void Enter()
        {
            Vector2 range = boss.Guard.Settings.GuardAdvanceDurationRange;
            _duration = Random.Range(range.x, range.y);
            _timer = 0f;
            DevLog.Log($"[TutorialBoss] 構えてにじり寄る（{_duration:0.0}秒）");

            boss.Guard.IsGuarding = true;
            boss.Animator.SetBool(IsGuardAdvancingHash, true);

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.speed = boss.Guard.Settings.GuardAdvanceSpeed;
                boss.SetAgentStopped(false);
                // 自前でプレイヤーへ向いたまま歩く
                boss.Agent.updateRotation = false;
            }
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            if (boss.Perception.IsPlayerTracked())
            {
                boss.LostSightTimer = 0f;
            }
            else
            {
                boss.LostSightTimer += Time.deltaTime;
                if (boss.LostSightTimer >= LostSightGraceBeforeChase)
                {
                    boss.ChangeState(new TBossChaseState(boss, continueLostSightTimer: true));
                    return;
                }
            }

            Vector3 playerPosition = boss.Player.transform.position;
            FaceTowards(playerPosition, RotateSpeed);
            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.SetDestination(playerPosition);
                UpdateStepSpeed();
            }

            // プレイヤーが攻撃の間合いへ入ってきたら攻撃する（クールダウン中なら構えたまま寄り続ける）
            float distance = Vector3.Distance(boss.transform.position, playerPosition);
            if (distance <= boss.NormalAttack.Range && boss.TryStartAttack())
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _duration)
            {
                // 入ってこないなら自分から攻撃に入る（走り込み・二段攻撃・ワイヤーから選ぶ）
                boss.TransitionToAttack();
            }
        }

        public override void Exit()
        {
            boss.Guard.IsGuarding = false;
            boss.Animator.SetBool(IsGuardAdvancingHash, false);
            boss.Animator.SetFloat(GuardAdvanceSpeedHash, 1f);

            if (boss.Agent != null)
            {
                boss.Agent.updateRotation = true;
            }
        }

        /// <summary>
        /// モーションの再生速度を、実際に進んでいる速さに合わせる。接地中の足は一定の速さで後ろへ送られる作りなので、
        /// 速さが合っていれば足は地面に止まって見える（止まれば足運びも止まる）。
        /// </summary>
        private void UpdateStepSpeed()
        {
            Vector3 velocity = boss.Agent.velocity;
            velocity.y = 0f;
            boss.Animator.SetFloat(
                GuardAdvanceSpeedHash,
                velocity.magnitude / boss.Guard.Settings.GuardAdvanceClipSpeed
            );
        }
    }
}
