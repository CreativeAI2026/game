using UnityEngine;

namespace CreativeAI.Gameplay
{
    // ─────────────────────────────────────────────────────────────────────────
    // 基底ステート
    // ─────────────────────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────
    // 待機ステート
    // ─────────────────────────────────────────────────────────────────────────
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
            // プレイヤーが視界に入ったら追跡開始
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

    // ─────────────────────────────────────────────────────────────────────────
    // 追跡ステート
    // 用途: 射線が切れているとき、またはプレイヤーが遠すぎるときに追跡して射線を作る
    // ─────────────────────────────────────────────────────────────────────────
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

            // 毎フレーム、プレイヤーの場所を目的地に設定
            if (testCon.Agent != null)
            {
                testCon.Agent.SetDestination(testCon.Player.transform.position);
            }

            // 一定距離以内に入り、かつ射線が確保できたら旋回ステートへ移行
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

    // ─────────────────────────────────────────────────────────────────────────
    // 旋回ステート
    // 用途: プレイヤーと一定距離を保ちながら旋回。
    //       - 一定時間経過 → ChaseState（攻撃サイクル再開）
    //       - 近づかれた  → 確率で BackStepState、外れたら低速後退
    // ─────────────────────────────────────────────────────────────────────────
    public class TestEnemyStrafeState : TestEnemyBaseState
    {
        private float _strafeTimer;

        // Enter 時に決定する旋回方向（+1: 右, -1: 左）
        private float _strafeDirection;

        public TestEnemyStrafeState(TestEnemyController core)
            : base(core) { }

        public override void Enter()
        {
            Debug.Log("旋回ステート開始");
            _strafeTimer = 0f;

            // 旋回方向をランダムに決定
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

            // 一定時間経過したら ChaseState へ（攻撃サイクル再開）
            if (_strafeTimer >= testCon.StrafeDuration)
            {
                testCon.ChangeState(new TestEnemyApproachState(testCon));
                return;
            }

            float distance = Vector3.Distance(
                testCon.transform.position,
                testCon.Player.transform.position
            );

            // 近づかれすぎた場合の対処
            if (distance <= testCon.BackStepRange)
            {
                if (Random.value <= testCon.BackStepChance)
                {
                    // 確率で BackStepState へ
                    testCon.ChangeState(new TestEnemyBackStepState(testCon));
                    return;
                }
                else
                {
                    // 確率が外れたら低速後退
                    Retreat();
                    return;
                }
            }

            // プレイヤーを向きながら横方向に旋回移動
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

        // プレイヤーを中心に旋回する移動計算
        private void Strafe()
        {
            if (testCon.Agent == null)
            {
                return;
            }

            Vector3 dirToPlayer = (
                testCon.Player.transform.position - testCon.transform.position
            ).normalized;

            // プレイヤーへの方向に対して垂直な軸（旋回方向）
            Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;

            // 目的地 = 現在位置 + 旋回方向 * 少し先
            Vector3 targetPos = testCon.transform.position + strafeDir * 2f;
            testCon.Agent.SetDestination(targetPos);

            // プレイヤーの方向を向く
            testCon.transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }

        // プレイヤーより遅いスピードで後退
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

    // ─────────────────────────────────────────────────────────────────────────
    // 接近ステート
    // 用途: 攻撃間合い（attackRange）に入るための接近。到達で AttackState へ。
    // ─────────────────────────────────────────────────────────────────────────
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

            // 攻撃間合いに入ったら AttackState へ
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

    // ─────────────────────────────────────────────────────────────────────────
    // 攻撃ステート
    // 用途: 攻撃を実行する。
    //       攻撃完了後:
    //         - 間合い内 → 確率で BackStepState、外れたら StrafeState
    //         - 間合い外 → ChaseState
    // ─────────────────────────────────────────────────────────────────────────
    public class TestEnemyAttackState : TestEnemyBaseState
    {
        // 振りかぶり中にプレイヤーを追尾する（旋回する）割合（0.0 ～ 1.0）
        // 0.3f なら、アニメーションの30%の時点までプレイヤーを追いかけます。
        private const float HomingThreshold = 0.3f;

        // 旋回の速さ
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
                return;

            // Animatorの現在再生されているステートの情報を取得（0はベースレイヤー）
            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);

            // アニメーションが「Attack」ステートに入っているか確認
            if (stateInfo.IsName("Attack"))
            {
                if (stateInfo.normalizedTime < HomingThreshold && testCon.Player != null)
                {
                    // プレイヤーの方向を計算（上下の傾きは無視する）
                    Vector3 dirToPlayer = (
                        testCon.Player.transform.position - testCon.transform.position
                    ).normalized;
                    dirToPlayer.y = 0f;

                    if (dirToPlayer != Vector3.zero)
                    {
                        // Quaternion.Slerp を使って、滑らかにプレイヤーの方へ振り向く
                        Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                        testCon.transform.rotation = Quaternion.Slerp(
                            testCon.transform.rotation,
                            targetRotation,
                            Time.deltaTime * HomingSpeed
                        );
                    }
                }

                // アニメーションが最後まで再生されきっていない場合はここで処理を止める（待機）
                if (stateInfo.normalizedTime < 1.0f)
                {
                    return;
                }

                // ─── ここから下はアニメーションが完了（1.0f以上）した時だけ実行される ───

                if (testCon.Player == null)
                {
                    testCon.ChangeState(new TestEnemyIdleState(testCon));
                    return;
                }

                float distance = Vector3.Distance(
                    testCon.transform.position,
                    testCon.Player.transform.position
                );

                // 攻撃完了後の次の行動を決定
                if (distance <= testCon.AttackRange)
                {
                    // 間合い内: 確率で BackStep、それ以外は Strafe
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
                    // 間合い外: 追跡に戻る
                    testCon.ChangeState(new TestEnemyChaseState(testCon));
                }
            }
        }

        public override void Exit()
        {
            Debug.Log("攻撃ステート終了");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // バックステップステート
    // 用途: 後退して間合いを開ける。終了後 StrafeState へ。
    // ─────────────────────────────────────────────────────────────────────────
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
                testCon.Agent.updateRotation = false; // 自動回転を無効化
            }

            // 開始時にプレイヤーの方向を向く
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

            // プレイヤーの方向を向き続ける
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

            // 後退するアクティブ時間（全体の50%を空中・ジャンプ中とみなす）
            float activeDuration = testCon.BackStepDuration * 0.5f;

            if (_backStepTimer < activeDuration)
            {
                // ジャンプ中：素早く後退方向へ移動
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
                // 着地後：後退を停止
                if (testCon.Agent != null)
                {
                    testCon.Agent.ResetPath();
                }
            }

            // 一定時間経過したら旋回ステートへ
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
                testCon.Agent.updateRotation = true; // 自動回転を有効化に戻す
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 怯みステート
    // 用途: ダメージを受けた時に強制的に移行し、一定時間行動不能になる
    // ─────────────────────────────────────────────────────────────────────────
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
                return;

            // Animatorの現在再生されているステートの情報を取得する（0はベースレイヤー）
            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);

            // アニメーションのステート名がFlinchである必要がある
            if (stateInfo.IsName("Flinch"))
            {
                // normalizedTime は 0.0=開始、1.0=100%完了（1周） を意味する
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

    // ─────────────────────────────────────────────────────────────────────────
    // パリィされ（大怯み）ステート
    // 用途: プレイヤーのジャストパリィ成功時に移行し、通常の怯みより長く隙を晒す
    // ─────────────────────────────────────────────────────────────────────────
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
                // ★ジャストパリィされた時専用のトリガー
                testCon.Animator.SetTrigger("Parried");
            }
        }

        public override void Update()
        {
            if (testCon.Animator == null)
                return;

            AnimatorStateInfo stateInfo = testCon.Animator.GetCurrentAnimatorStateInfo(0);

            // アニメーションのステート名が Parried である必要がある
            if (stateInfo.IsName("Parried"))
            {
                // 怯みアニメーションが完全に終わったらIdleへ戻る
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
