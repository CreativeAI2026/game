using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 踏み込んで振る近接攻撃ステート。当たり判定はAnimationEventで制御する。
    ///
    /// クリップ（TBoss_Attack1）自体に約2.6mの踏み込みが入っているため、ルートを動かすのは「前足が浮いて前へ出る区間」だけにし、
    /// 動かす長さもプレイヤーとの距離に合わせて足りない分だけにする（距離合わせ）。
    /// 着地後にルートを動かすと、接地した足ごと地面を滑って見えるため動かさない。当たり判定は大きくしない。
    /// </summary>
    public class TBossNormalAttackState : TBossBaseState
    {
        // プレイヤーへ向き直る区間の終わり（クリップの正規化時間）。
        // 計測では振りが前へ出てくるのが0.46付近のため、その直前まで向き直り、振り始めてからは追わない（避けられるようにする）
        private const float HomingEndTime = 0.44f;

        // 前足（左足）が浮いて前へ踏み出す区間（クリップの正規化時間、計測値）。この区間だけルートを動かす
        private const float StepInStartTime = 0.4f;
        private const float StepInEndTime = 0.475f;

        // 踏み込み後に、体が触れる距離からさらに空ける余裕（m）。めり込むと衝突を戻した時にプレイヤーが押し出されるため
        private const float StepInContactMargin = 0.5f;

        private const float HomingSpeed = 10f;

        // Attackステートに入らないままこの時間を超えたら、トリガー取りこぼしとみなして復帰する
        private const float AttackStateTimeout = 3f;

        private TBossMeleeApproach _approach;
        private bool _isApproaching;
        private float _attackTimer;

        // 踏み込む長さ（踏み込み区間に入った瞬間に決める）と、ここまでに動かした長さ
        private bool _hasPlannedStepIn;
        private float _stepInDistance;
        private float _steppedDistance;

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
                boss.SetAgentStopped(true);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Attack");
            }

            _hasPlannedStepIn = false;
            _steppedDistance = 0f;
            boss.IgnorePlayerCollision();
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

            float normalizedTime = stateInfo.normalizedTime;

            if (normalizedTime < HomingEndTime && boss.Player != null)
            {
                FacePlayer();
            }

            // ステートの Update は Animator がこのフレームの姿勢を進める前に走るため、
            // 判定は「このフレームで表示される姿勢の時刻」で行う（今の時刻で判定すると、着地した姿勢で動かしてしまう）
            float renderedTime =
                stateInfo.length > 0f
                    ? normalizedTime + Time.deltaTime * boss.Animator.speed / stateInfo.length
                    : normalizedTime;
            if (renderedTime >= StepInStartTime)
            {
                UpdateStepIn(renderedTime);
            }

            // アニメーション終了後の遷移
            if (stateInfo.normalizedTime >= 1.0f)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        private void FacePlayer()
        {
            Vector3 dirToPlayer = boss.Player.transform.position - boss.transform.position;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(dirToPlayer.normalized),
                Time.deltaTime * HomingSpeed
            );
        }

        /// <summary>
        /// 前足が浮いている区間だけ、決めた長さをクリップの進み具合に合わせて少しずつ前へ動かす。着地後は動かさない。
        /// フレームが粗くて区間の終わりを飛び越えた場合、残りは着地した足ごと滑らせることになるため動かさずに捨てる
        /// （少し短く踏み込むだけで、当たり判定の余裕で届く）。
        /// </summary>
        private void UpdateStepIn(float renderedTime)
        {
            if (renderedTime > StepInEndTime)
            {
                return;
            }

            if (!_hasPlannedStepIn)
            {
                _stepInDistance = PlanStepInDistance();
                _hasPlannedStepIn = true;
            }

            float progress = Mathf.InverseLerp(StepInStartTime, StepInEndTime, renderedTime);
            float delta = _stepInDistance * progress - _steppedDistance;
            if (delta <= 0f || boss.Agent == null)
            {
                return;
            }

            boss.Agent.Move(boss.transform.forward * delta);
            _steppedDistance += delta;
        }

        /// <summary>
        /// 踏み込む長さ。プレイヤーが狙いの距離（Attack1StrikeDistance）に来るのに足りない分だけ踏み込み、
        /// 上限（Attack1MaxStepIn）と「体が触れる距離＋余裕」より近づかない長さで止める。
        /// </summary>
        private float PlanStepInDistance()
        {
            if (boss.Player == null)
            {
                return 0f;
            }

            Vector3 toPlayer = boss.Player.transform.position - boss.transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            float toStrike = distance - boss.Attack1StrikeDistance;
            float toContact = distance - (boss.BodyContactDistance + StepInContactMargin);
            return Mathf.Clamp(Mathf.Min(toStrike, toContact), 0f, boss.Attack1MaxStepIn);
        }

        public override void Exit()
        {
            // 重なったまま衝突を戻すとプレイヤーが押し出されて上へ飛ぶため、離れてから戻す
            boss.RestorePlayerCollisionWhenSeparated();

            if (boss.Agent != null)
            {
                boss.SetAgentStopped(false);
            }

            // 怯み・パリィなどで振りの途中に抜けた場合でも、攻撃判定と軌跡を残さない
            boss.StopSwingEffects();
        }
    }
}
