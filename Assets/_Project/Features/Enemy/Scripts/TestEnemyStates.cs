using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ステートパターンを用いたAI行動制御の基底クラス。
    /// 各状態間で使用するコンテキスト（TestEnemyController）を共有し、状態遷移をカプセル化する。
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
    /// 未発見状態でのアイドリングを表現するステート。不要な計算負荷を抑えるための初期状態。
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
            if (testCon.Player != null && (testCon.IsAlerted || testCon.CheckInSight()))
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
    /// 発見したプレイヤーとの距離を素早く詰め、各種戦闘行動（旋回、攻撃など）へ移行するための繋ぎのステート。
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

            if (
                distance >= testCon.NeedleAttackDistance
                && testCon.NeedleAttackTimer <= 0f
                && testCon.CheckInSight()
            )
            {
                testCon.ChangeState(new TestEnemyNeedleAttackState(testCon));
                return;
            }

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
    /// 単調な直線的追尾を避け、プレイヤーに横方向へのエイムや立ち回りを要求するための立ち回りステート。
    /// </summary>
    public class TestEnemyStrafeState : TestEnemyBaseState
    {
        private float _strafeTimer;

        private float _strafeDirection;

        public TestEnemyStrafeState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
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

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );

            if (
                distance >= testCon.NeedleAttackDistance
                && testCon.NeedleAttackTimer <= 0f
                && testCon.CheckInSight()
            )
            {
                testCon.ChangeState(new TestEnemyNeedleAttackState(testCon));
                return;
            }

            if (_strafeTimer >= testCon.StrafeDuration)
            {
                testCon.ChangeState(new TestEnemyApproachState(testCon));
                return;
            }

            // 至近距離でのプレイヤーの猛攻を回避し、AIが有利な間合いを自律的に維持するため
            if (distance <= testCon.BackStepRange)
            {
                if (Random.value <= testCon.BackStepChance)
                {
                    testCon.ChangeState(new TestEnemyBackStepState(testCon));
                    return;
                }
                else
                {
                    testCon.ChangeState(new TestEnemyRetreatState(testCon));
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

            // NavMesh外や障害物にスタックして不自然な足踏み挙動になるのを防ぐため、進行方向に壁があれば反転させる
            Vector3 rayStart = testCon.transform.position + Vector3.up * 1f;
            if (Physics.Raycast(rayStart, strafeDir, out RaycastHit hit, 2f, testCon.ObstacleLayer))
            {
                _strafeDirection *= -1f; // 方向反転
                strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
            }

            // 2fはNavMeshAgentが次フレームまでに到達しうる十分な距離として設定
            Vector3 targetPos = testCon.transform.position + strafeDir * 2f;
            testCon.Agent.SetDestination(targetPos);
            testCon.transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }
    }

    /// <summary>
    /// 攻撃直前の予備動作として、意図的にプレイヤーへプレッシャーを与えつつ攻撃レンジへ誘導するステート。
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
    /// 近接攻撃の実行ステート。プレイヤーの回避行動に対応するためのホーミングと、
    /// 攻撃時の不自然なスライディングを防ぐための踏み込み制御を切り替えて運用する。
    /// </summary>
    public class TestEnemyAttackState : TestEnemyBaseState
    {
        // プレイヤーの回避タイミングをシビアにし、攻撃を当てやすくするためのホーミング猶予区間
        private const float HomingThreshold = 0.4f;

        // 攻撃判定発生時の不自然な旋回（スライディング）を防ぎ、慣性を表現するための踏み込み区間
        private const float LungeStartThreshold = 0.4f;
        private const float LungeEndThreshold = 0.7f;

        private const float HomingSpeed = 10f;
        private const float LungeSpeed = 15f; // 貫通するように大きく前進させるため速度を上げる

        public TestEnemyAttackState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("Attack");
            }

            if (testCon.EnemyCollider != null && testCon.PlayerCollider != null)
            {
                // 踏み込みでプレイヤーが押されることを防ぐために、貫くような攻撃をさせる。
                Physics.IgnoreCollision(testCon.EnemyCollider, testCon.PlayerCollider, true);
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
                else if (
                    stateInfo.normalizedTime >= LungeStartThreshold
                    && stateInfo.normalizedTime <= LungeEndThreshold
                )
                {
                    if (testCon.Agent != null)
                    {
                        testCon.Agent.Move(
                            testCon.transform.forward * (LungeSpeed * Time.deltaTime)
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
            if (testCon.EnemyCollider != null && testCon.PlayerCollider != null)
            {
                Physics.IgnoreCollision(testCon.EnemyCollider, testCon.PlayerCollider, false);
            }
        }
    }

    /// <summary>
    /// プレイヤーの近接攻撃に対する防御的な立ち回りとして、瞬間的に間合いをリセットし態勢を立て直すステート。
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

            // モーションの視覚的な接地感（足の滑り）を損なわないよう、後半の着地・復帰モーション中は移動入力を切る
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
    /// バックステップのみでは単調になるため、異なるテンポでの距離調整手段を提供しプレイヤーの予測を外すステート。
    /// </summary>
    public class TestEnemyRetreatState : TestEnemyBaseState
    {
        private float _retreatTimer;

        public TestEnemyRetreatState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            _retreatTimer = 0f;
            if (testCon.Agent != null)
                testCon.Agent.speed = testCon.BackStepSpeed;
        }

        public override void Update()
        {
            if (testCon.Player == null)
                return;
            _retreatTimer += Time.deltaTime;

            Vector3 retreatDir = (
                testCon.transform.position - testCon.Player.transform.position
            ).normalized;
            Vector3 retreatTarget = testCon.transform.position + retreatDir * 2f;

            if (testCon.Agent != null)
                testCon.Agent.SetDestination(retreatTarget);
            testCon.transform.rotation = Quaternion.LookRotation(-retreatDir); // プレイヤーを向く

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );

            if (distance > testCon.BackStepRange + 1f || _retreatTimer > 1.5f)
            {
                testCon.ChangeState(new TestEnemyStrafeState(testCon));
            }
        }

        public override void Exit()
        {
            if (testCon.Agent != null)
                testCon.Agent.ResetPath();
        }
    }

    /// <summary>
    /// 攻撃をヒットさせたプレイヤーへ視覚的なフィードバックを与え、明確な反撃のチャンスを確保するステート。
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
                    if (testCon.IsAlerted)
                        testCon.ChangeState(new TestEnemyChaseState(testCon));
                    else
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
                    if (testCon.IsAlerted)
                        testCon.ChangeState(new TestEnemyChaseState(testCon));
                    else
                        testCon.ChangeState(new TestEnemyIdleState(testCon));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("敵：大怯みステート終了");
        }
    }

    /// <summary>
    /// AI制御と物理演算を完全に停止させ、死体としての挙動を確定させるための終端ステート。
    /// </summary>
    public class TestEnemyDeathState : TestEnemyBaseState
    {
        public TestEnemyDeathState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("死亡ステート開始");

            if (testCon.Agent != null)
                testCon.Agent.enabled = false;
            if (testCon.EnemyCollider != null)
                testCon.EnemyCollider.enabled = false;

            if (testCon.Animator != null)
            {
                testCon.Animator.SetTrigger("Die");
            }
        }

        public override void Update() { }
    }

    /// <summary>
    /// 遠距離から広範囲に弾幕を展開し、プレイヤーに強制的に回避行動やパリィを要求するためのステート。
    /// </summary>
    public class TestEnemyNeedleAttackState : TestEnemyBaseState
    {
        private float _timer;
        private List<EnemyNeedleProjectile> _needles = new List<EnemyNeedleProjectile>();
        private float _fireInterval = 0.2f;
        private float _riseAimDuration = 1.5f;

        public TestEnemyNeedleAttackState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("針攻撃ステート開始");
            _timer = 0f;

            if (testCon.Agent != null)
            {
                testCon.Agent.ResetPath();
            }

            if (testCon.Animator != null)
            {
                testCon.Animator.SetBool("IsRunning", false);
                testCon.Animator.SetTrigger("Roar");
            }

            testCon.NeedleAttackTimer = testCon.NeedleAttackCooldown;

            float angleStep = 360f / testCon.NeedleCount;
            for (int i = 0; i < testCon.NeedleCount; i++)
            {
                GameObject needleObj;
                if (testCon.needlePrefab != null)
                {
                    needleObj = Object.Instantiate(testCon.needlePrefab);
                }
                else
                {
                    needleObj = new GameObject("NeedleProjectile");
                }

                var needle = needleObj.GetComponent<EnemyNeedleProjectile>();
                if (needle == null)
                {
                    needle = needleObj.AddComponent<EnemyNeedleProjectile>();
                }

                // 弾幕の密度を調整し、プレイヤーに連続回避の猶予を与えるためのオフセット時間
                float delay = i * _fireInterval;
                needle.Initialize(
                    testCon.transform,
                    testCon.Player.transform,
                    angleStep * i,
                    delay,
                    testCon.NeedleDamage
                );
                _needles.Add(needle);
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            // 攻撃の予備動作中にターゲットを見失い、明後日の方向に発射してしまうのを防ぐため
            if (testCon.Player != null)
            {
                Vector3 dirToPlayer = (
                    testCon.Player.transform.position - testCon.transform.position
                ).normalized;
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero)
                {
                    testCon.transform.rotation = Quaternion.Slerp(
                        testCon.transform.rotation,
                        Quaternion.LookRotation(dirToPlayer),
                        Time.deltaTime * 5f
                    );
                }
            }

            // 全ての針の射出シーケンスが完了するまでステートを維持し、途中で別の行動に割り込まれないようにするため
            float totalDuration = _riseAimDuration + (testCon.NeedleCount * _fireInterval) + 1.0f;

            if (_timer >= totalDuration)
            {
                if (testCon.IsAlerted)
                    testCon.ChangeState(new TestEnemyChaseState(testCon));
                else
                    testCon.ChangeState(new TestEnemyIdleState(testCon));
            }
        }

        public override void Exit()
        {
            Debug.Log("針攻撃ステート終了");
        }
    }
}
