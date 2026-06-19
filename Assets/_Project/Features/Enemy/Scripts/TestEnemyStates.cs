using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// テスト用敵の各ステート共通基底。TestEnemyControllerへの参照を保持する。
    /// </summary>
    public class TestEnemyBaseState : IEnemyState
    {
        protected TestEnemyController testCon;

        public TestEnemyBaseState(TestEnemyController controller)
        {
            testCon = controller;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void Exit() { }
    }

    /// <summary>
    /// 待機ステート。プレイヤーが視界に入るまでこのステートに留まる。
    /// </summary>
    public class TestEnemyIdleState : TestEnemyBaseState
    {
        public TestEnemyIdleState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("待機ステート開始");
        }

        public override void Update()
        {
            if (testCon.CheckInSight())
            {
                testCon.IsAlerted = true;
                testCon.ChangeState(new TestEnemyChaseState(testCon));
            }
        }

        public override void Exit()
        {
            Debug.Log("待機ステート終了");
        }
    }

    /// <summary>
    /// 追跡ステート。プレイヤーをNavMeshで追いかけ、旋回範囲に入ったら戦闘行動に移行する。
    /// </summary>
    public class TestEnemyChaseState : TestEnemyBaseState
    {
        public TestEnemyChaseState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("追跡ステート開始");
            if (testCon.Agent != null)
            {
                testCon.Agent.speed = testCon.ChaseSpeed;
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetBool("IsRunning", true);
            }
        }

        public override void Update()
        {
            if (testCon.Player == null)
            {
                return;
            }

            if (testCon.Agent != null)
            {
                testCon.Agent.SetDestination(testCon.Player.transform.position);
            }

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );
            if (distance <= testCon.StrafeRange && testCon.CheckInSight())
            {
                testCon.ChangeState(new TestEnemyStrafeState(testCon));
            }
        }

        public override void Exit()
        {
            Debug.Log("追跡ステート終了");
            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetBool("IsRunning", false);
            }
        }
    }

    /// <summary>
    /// 旋回（ストレイフ）ステート。プレイヤーの周囲を横移動し、隙を窺う。
    /// 一定時間経過後に接近ステートへ遷移する。
    /// </summary>
    public class TestEnemyStrafeState : TestEnemyBaseState
    {
        private float _strafeTimer;

        private float _strafeDirection;

        public TestEnemyStrafeState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("旋回ステート開始");
            _strafeTimer = 0f;

            // ランダムに左右を決めることで、複数の敵が同じ方向に旋回し続けるのを防ぐ
            _strafeDirection = Random.value > 0.5f ? 1f : -1f;

            if (testCon.Agent != null)
            {
                testCon.Agent.speed = testCon.StrafeSpeed;
            }
        }

        public override void Update()
        {
            if (testCon.Player == null)
            {
                return;
            }

            _strafeTimer += Time.deltaTime;

            if (_strafeTimer >= testCon.StrafeDuration)
            {
                testCon.ChangeState(new TestEnemyApproachState(testCon));
                return;
            }

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );

            // プレイヤーが接近しすぎた場合、バックステップか後退で距離を取り直す
            if (distance <= testCon.BackStepRange)
            {
                if (Random.value <= testCon.BackStepChance)
                {
                    testCon.ChangeState(new TestEnemyBackStepState(testCon));
                    return;
                }
                else
                {
                    Retreat();
                    return;
                }
            }

            Strafe();
        }

        public override void Exit()
        {
            Debug.Log("旋回ステート終了");
            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }
        }

        private void Strafe()
        {
            if (testCon.Agent == null)
            {
                return;
            }

            Vector3 dirToPlayer = (
                testCon.Player.transform.position - testCon.transform.position
            ).normalized;

            Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;

            // 2fはNavMeshAgentが次フレームまでに到達しうる十分な距離として設定
            Vector3 targetPos = testCon.transform.position + strafeDir * 2f;
            testCon.Agent.SetDestination(targetPos);

            testCon.transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }

        private void Retreat()
        {
            if (testCon.Agent == null)
            {
                return;
            }

            testCon.Agent.speed = testCon.BackStepSpeed;
            Vector3 retreatDir = (
                testCon.transform.position - testCon.Player.transform.position
            ).normalized;
            Vector3 retreatTarget = testCon.transform.position + retreatDir * 2f;
            testCon.Agent.SetDestination(retreatTarget);
        }
    }

    /// <summary>
    /// 接近ステート。攻撃範囲までプレイヤーに向かって直進する。
    /// </summary>
    public class TestEnemyApproachState : TestEnemyBaseState
    {
        public TestEnemyApproachState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("接近ステート開始");
            if (testCon.Agent != null)
            {
                testCon.Agent.speed = testCon.ApproachSpeed;
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetBool("IsRunning", true);
            }
        }

        public override void Update()
        {
            if (testCon.Player == null)
            {
                return;
            }

            if (testCon.Agent != null)
            {
                testCon.Agent.SetDestination(testCon.Player.transform.position);
            }

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );

            if (distance <= testCon.AttackRange)
            {
                testCon.ChangeState(new TestEnemyAttackState(testCon));
            }
        }

        public override void Exit()
        {
            Debug.Log("接近ステート終了");
            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetBool("IsRunning", false);
            }
        }
    }

    /// <summary>
    /// 攻撃ステート。攻撃アニメーション中はプレイヤー方向へホーミング回転し、
    /// アニメーション完了後に距離に応じて次の行動を決定する。
    /// </summary>
    public class TestEnemyAttackState : TestEnemyBaseState
    {
        // 攻撃アニメーションの最初30%区間のみホーミングを有効にする。
        // 振り終わりまでホーミングすると不自然な追尾になるため。
        private const float HomingThreshold = 0.3f;

        private const float HomingSpeed = 10f;

        public TestEnemyAttackState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("攻撃ステート開始");

            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("Attack");
            }
        }

        public override void Update()
        {
            if (testCon.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Attack"))
            {
                if (stateInfo.normalizedTime < HomingThreshold && testCon.Player != null)
                {
                    Vector3 dirToPlayer = (
                        testCon.Player.transform.position - testCon.transform.position
                    ).normalized;
                    dirToPlayer.y = 0f;

                    if (dirToPlayer != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                        testCon.transform.rotation = Quaternion.Slerp(
                            testCon.transform.rotation,
                            targetRotation,
                            Time.deltaTime * HomingSpeed
                        );
                    }
                }

                if (stateInfo.normalizedTime < 1.0f)
                {
                    return;
                }

                if (testCon.Player == null)
                {
                    testCon.ChangeState(new TestEnemyIdleState(testCon));
                    return;
                }

                float distance = Vector3.Distance(
                    testCon.transform.position,
                    testCon.Player.transform.position
                );

                // 攻撃後の次の行動をランダム性を持たせて選択し、パターン化を防ぐ
                if (distance <= testCon.AttackRange)
                {
                    if (Random.value <= testCon.BackStepChance)
                    {
                        testCon.ChangeState(new TestEnemyBackStepState(testCon));
                    }
                    else
                    {
                        testCon.ChangeState(new TestEnemyStrafeState(testCon));
                    }
                }
                else
                {
                    testCon.ChangeState(new TestEnemyChaseState(testCon));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("攻撃ステート終了");
        }
    }

    /// <summary>
    /// バックステップステート。プレイヤーの方を向いたまま後退し距離を取る。
    /// </summary>
    public class TestEnemyBackStepState : TestEnemyBaseState
    {
        private float _backStepTimer;

        public TestEnemyBackStepState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("バックステップステート開始");
            _backStepTimer = 0f;

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("BackStep");
            }

            if (testCon.Agent != null)
            {
                testCon.Agent.speed = testCon.BackStepSpeed;
                // バックステップ中はプレイヤーの方を向き続けるため、NavMeshの自動回転を無効にする
                testCon.Agent.updateRotation = false;
            }

            if (testCon.Player != null)
            {
                Vector3 dirToPlayer = (
                    testCon.Player.transform.position - testCon.transform.position
                ).normalized;
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero)
                {
                    testCon.transform.rotation = Quaternion.LookRotation(dirToPlayer);
                }
            }
        }

        public override void Update()
        {
            _backStepTimer += Time.deltaTime;

            if (testCon.Player != null)
            {
                Vector3 dirToPlayer = (
                    testCon.Player.transform.position - testCon.transform.position
                ).normalized;
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero)
                {
                    testCon.transform.rotation = Quaternion.LookRotation(dirToPlayer);
                }
            }

            // 前半は実際に後退移動し、後半は着地・復帰モーションのため停止する
            float activeDuration = testCon.BackStepDuration * 0.5f;

            if (_backStepTimer < activeDuration)
            {
                if (testCon.Agent != null && testCon.Player != null)
                {
                    Vector3 retreatDir = (
                        testCon.transform.position - testCon.Player.transform.position
                    ).normalized;
                    Vector3 retreatTarget = testCon.transform.position + retreatDir * 2f;
                    testCon.Agent.SetDestination(retreatTarget);
                }
            }
            else
            {
                if (testCon.Agent != null)
                {
                    testCon.Agent.ResetPath();
                }
            }

            if (_backStepTimer >= testCon.BackStepDuration)
            {
                testCon.ChangeState(new TestEnemyStrafeState(testCon));
            }
        }

        public override void Exit()
        {
            Debug.Log("バックステップステート終了");
            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
                testCon.Agent.updateRotation = true;
            }
        }
    }

    /// <summary>
    /// 怯みステート。被弾時に行動を中断し、怯みアニメーション完了後にIdleへ戻る。
    /// </summary>
    public class TestEnemyFlinchState : TestEnemyBaseState
    {
        public TestEnemyFlinchState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("怯みステート開始");

            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
                testCon.Agent.velocity = Vector3.zero;
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("Flinch");
            }
        }

        public override void Update()
        {
            if (testCon.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);

            // Animatorのステート名と一致させる必要がある（変更不可）
            if (stateInfo.IsName("Flinch"))
            {
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    testCon.ChangeState(new TestEnemyIdleState(testCon));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("怯みステート終了");
        }
    }

    /// <summary>
    /// ジャストパリィ被弾ステート。通常の怯みより長い硬直を持ち、プレイヤーに反撃の隙を与える。
    /// </summary>
    public class TestEnemyParriedState : TestEnemyBaseState
    {
        public TestEnemyParriedState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("敵：ジャストパリィによる大怯みステート開始");

            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
                testCon.Agent.velocity = Vector3.zero;
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("Parried");
            }
        }

        public override void Update()
        {
            if (testCon.Animator == null)
                return;

            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);

            // Animatorのステート名と一致させる必要がある（変更不可）
            if (stateInfo.IsName("Parried"))
            {
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    testCon.ChangeState(new TestEnemyIdleState(testCon));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("敵：大怯みステート終了");
        }
    }
}
