using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 焦り度の各指標の判定（何を数え、何を除くか）の検証。
    /// EditMode では Time.time が進まないため、時間経過は Tick の deltaTime で与え、
    /// 時刻で数える指標は「同じ時刻に何回起きたか」で検証する。
    /// </summary>
    public class PanicSignalTests
    {
        private const float WalkSpeed = 5f;
        private const float Step = 0.1f;

        /// <summary>攻撃中かどうかをテストから決められる敵。</summary>
        private class FakeEnemy : EnemyBaseController
        {
            public bool Attacking { get; set; }

            public override bool IsAttacking => Attacking;
        }

        private GameObject _enemyGo;
        private FakeEnemy _enemy;

        [SetUp]
        public void SetUp()
        {
            _enemyGo = new GameObject("FakeEnemy");
            _enemy = _enemyGo.AddComponent<FakeEnemy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_enemyGo);
        }

        /// <summary>敵が +Z 方向 5m にいる交戦中の観測値。</summary>
        private PanicContext CombatContext()
        {
            return new PanicContext
            {
                EngagedEnemy = _enemy,
                ToEnemyDirectionXZ = Vector3.forward,
                EnemyDistance = 5f,
                WalkSpeed = WalkSpeed,
                CanMove = true,
                MoveInput = Vector2.down,
                CameraForwardXZ = Vector3.forward,
            };
        }

        private static void TickFor(PanicSignal signal, PanicContext context, float seconds)
        {
            for (float t = 0f; t < seconds - 0.0001f; t += Step)
            {
                signal.Tick(context, Step);
            }
        }

        // --- 逃避 ---

        [Test]
        public void 逃避_背を向けて離れると満点まで溜まる()
        {
            var flight = new FlightSignal();
            PanicContext context = CombatContext();
            context.HorizontalVelocity = Vector3.back * WalkSpeed;
            context.CameraForwardXZ = Vector3.back;

            TickFor(flight, context, 3f);

            Assert.That(flight.Score, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void 逃避_敵を向いたまま後ろへ下がると半分の速さで溜まる()
        {
            var flight = new FlightSignal();
            PanicContext context = CombatContext();
            context.HorizontalVelocity = Vector3.back * WalkSpeed;

            TickFor(flight, context, 3f);

            Assert.That(flight.Score, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void 逃避_弓を構えて下がる引き撃ちは数えない()
        {
            var flight = new FlightSignal();
            PanicContext context = CombatContext();
            context.HorizontalVelocity = Vector3.back * WalkSpeed;
            context.CameraForwardXZ = Vector3.back;
            context.IsAiming = true;

            TickFor(flight, context, 3f);

            Assert.That(flight.Score, Is.EqualTo(0f));
        }

        [Test]
        public void 逃避_横へ回り込むだけの移動は数えない()
        {
            var flight = new FlightSignal();
            PanicContext context = CombatContext();
            context.HorizontalVelocity = Vector3.right * WalkSpeed;
            context.CameraForwardXZ = Vector3.right;

            TickFor(flight, context, 3f);

            Assert.That(flight.Score, Is.EqualTo(0f));
        }

        [Test]
        public void 逃避_止まっても0に戻さずゆっくり減る()
        {
            var flight = new FlightSignal();
            PanicContext context = CombatContext();
            context.HorizontalVelocity = Vector3.back * WalkSpeed;
            context.CameraForwardXZ = Vector3.back;
            TickFor(flight, context, 3f);

            context.HorizontalVelocity = Vector3.zero;
            TickFor(flight, context, 1f);

            // 満点(3秒)から1秒分だけ減る
            Assert.That(flight.Timer, Is.EqualTo(2f).Within(0.01f));
        }

        // --- スパム ---

        [Test]
        public void スパム_ボスの攻撃中に押した分はパリィ狙いとして数えない()
        {
            var spam = new GuardSpamSignal();
            PanicContext context = CombatContext();
            _enemy.Attacking = true;

            for (int i = 0; i < 5; i++)
            {
                spam.RecordPress(context);
            }
            spam.Tick(context, Step);

            Assert.That(spam.Score, Is.EqualTo(0f));
        }

        [Test]
        public void スパム_何も来ていない時の連打は数え1回だけなら数えない()
        {
            var spam = new GuardSpamSignal();
            PanicContext context = CombatContext();

            spam.RecordPress(context);
            spam.Tick(context, Step);
            Assert.That(spam.Score, Is.EqualTo(0f));

            for (int i = 0; i < 4; i++)
            {
                spam.RecordPress(context);
            }
            spam.Tick(context, Step);

            // 1.5秒で5回 = 3.33回/秒 → 満点(3回/秒)で頭打ち
            Assert.That(spam.Score, Is.EqualTo(1f));
        }

        // --- ジッター ---

        [Test]
        public void ジッター_行き過ぎては戻す切り返しを数える()
        {
            var jitter = new AimJitterSignal();
            PanicContext context = CombatContext();
            context.IsAiming = true;

            // 0 → +5 → 0 → +5 → 0 → +5 と振ると、5度の振りの切り返しが4回
            float[] yaws = { 0f, 5f, 0f, 5f, 0f, 5f };
            foreach (float yaw in yaws)
            {
                context.CameraYaw = yaw;
                jitter.Tick(context, Step);
            }

            Assert.That(jitter.ReversalCount, Is.EqualTo(4));
            Assert.That(jitter.Score, Is.EqualTo(1f));
        }

        [Test]
        public void ジッター_一方向に滑らかに追う操作は数えない()
        {
            var jitter = new AimJitterSignal();
            PanicContext context = CombatContext();
            context.IsAiming = true;

            for (int i = 0; i <= 20; i++)
            {
                context.CameraYaw = i * 10f;
                jitter.Tick(context, Step);
            }

            Assert.That(jitter.ReversalCount, Is.EqualTo(0));
        }

        [Test]
        public void ジッター_細かな照準調整の切り返しは数えない()
        {
            var jitter = new AimJitterSignal();
            PanicContext context = CombatContext();
            context.IsAiming = true;

            // 1度ずつの往復は最小の振り(3度)に届かない
            float[] yaws = { 0f, 1f, 0f, 1f, 0f, 1f };
            foreach (float yaw in yaws)
            {
                context.CameraYaw = yaw;
                jitter.Tick(context, Step);
            }

            Assert.That(jitter.ReversalCount, Is.EqualTo(0));
        }

        // --- 空間喪失 ---

        /// <summary>左の壁へ向かって押しているのに動けない（敵は +Z 方向 5m）観測値。</summary>
        private PanicContext PushingIntoLeftWallContext()
        {
            PanicContext context = CombatContext();
            context.DesiredSpeed = WalkSpeed;
            context.DesiredMoveDirection = Vector3.left;
            context.HorizontalVelocity = Vector3.zero;
            return context;
        }

        [Test]
        public void 空間喪失_敵の近くで入力しているのに動けないと溜まる()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void 空間喪失_壁に沿って横へ滑っているだけでも狙った方向へ進めていなければ溜まる()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            // 左へ押しているが、壁に沿って後ろへ滑っている（速さはあるが狙った方向へは進んでいない）
            context.HorizontalVelocity = Vector3.back * 4f;

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void 空間喪失_斬られて怯んでいる間は溜まった時間を保つ()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            TickFor(trap, context, 2f);

            // 怯み・掴みで移動が封じられた（以前はここで2倍の速さで減っていた）
            context.CanMove = false;
            context.DesiredSpeed = 0f;
            context.DesiredMoveDirection = Vector3.zero;
            TickFor(trap, context, 2f);

            Assert.That(trap.Timer, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void 空間喪失_敵の体へ向かって押しているだけなら数えない()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            context.DesiredMoveDirection = Vector3.forward; // 敵の方向
            context.IsTouchingEnemy = true;

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(0f));
        }

        [Test]
        public void 空間喪失_敵に押されて壁際に挟まれているなら溜まる()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            context.DesiredMoveDirection = Vector3.back; // 敵から離れようとしている
            context.IsTouchingEnemy = true;

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void 空間喪失_敵が遠ければ数えない()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            context.EnemyDistance = 20f;

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(0f));
        }

        [Test]
        public void 空間喪失_スティックを少し倒してゆっくり歩いているだけなら数えない()
        {
            var trap = new TrapSignal();
            PanicContext context = PushingIntoLeftWallContext();
            // 期待速度 1m/s に対して、入力した方向へ 0.9m/s で進めている
            context.DesiredSpeed = 1f;
            context.HorizontalVelocity = Vector3.left * 0.9f;

            TickFor(trap, context, 3f);

            Assert.That(trap.Score, Is.EqualTo(0f));
        }

        // --- 硬直 ---

        /// <summary>敵の攻撃を1回分（始まり→続く→終わり）流す。攻撃の途中で duringAttack を1回呼ぶ。</summary>
        private void PlayEnemyAttack(
            FreezeSignal freeze,
            PanicContext context,
            System.Action duringAttack = null
        )
        {
            _enemy.Attacking = true;
            for (int i = 0; i < 5; i++)
            {
                freeze.Tick(context, Step);
                if (i == 2)
                {
                    duringAttack?.Invoke();
                }
            }
            _enemy.Attacking = false;
            freeze.Tick(context, Step);
        }

        [Test]
        public void 硬直_近くで攻撃されて何もしなければ1回と数える()
        {
            var freeze = new FreezeSignal();
            PanicContext context = CombatContext();
            context.MoveInput = Vector2.zero;

            PlayEnemyAttack(freeze, context);

            Assert.That(freeze.Count, Is.EqualTo(1));
            Assert.That(freeze.Score, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void 硬直_攻撃中にボタンを押して反応していれば数えない()
        {
            var freeze = new FreezeSignal();
            PanicContext context = CombatContext();
            context.MoveInput = Vector2.zero;

            PlayEnemyAttack(freeze, context, freeze.RecordPlayerAction);

            Assert.That(freeze.Count, Is.EqualTo(0));
        }

        [Test]
        public void 硬直_移動入力や自分の攻撃モーション中なら数えない()
        {
            var freeze = new FreezeSignal();
            PanicContext context = CombatContext();

            // 移動入力あり（CombatContext の既定）
            PlayEnemyAttack(freeze, context);

            context.MoveInput = Vector2.zero;
            context.IsBusyWithOwnAction = true;
            PlayEnemyAttack(freeze, context);

            Assert.That(freeze.Count, Is.EqualTo(0));
        }

        [Test]
        public void 硬直_遠くの攻撃に反応しないのは数えない()
        {
            var freeze = new FreezeSignal();
            PanicContext context = CombatContext();
            context.MoveInput = Vector2.zero;
            context.EnemyDistance = 30f;

            PlayEnemyAttack(freeze, context);

            Assert.That(freeze.Count, Is.EqualTo(0));
        }

        // --- 武器空回し ---

        [Test]
        public void 武器空回し_2回までは数えず3回で半分4回で満点()
        {
            var thrash = new WeaponThrashSignal();

            thrash.RecordSwitch();
            thrash.RecordSwitch();
            Assert.That(thrash.Score, Is.EqualTo(0f));

            thrash.RecordSwitch();
            Assert.That(thrash.Score, Is.EqualTo(0.5f).Within(0.001f));

            thrash.RecordSwitch();
            Assert.That(thrash.Score, Is.EqualTo(1f));
        }

        // --- 回避失敗 ---

        [Test]
        public void 回避失敗_1回目は数えず2回で半分3回で満点()
        {
            var hitChain = new HitChainSignal();

            hitChain.RecordHit();
            Assert.That(hitChain.Score, Is.EqualTo(0f));

            hitChain.RecordHit();
            Assert.That(hitChain.Score, Is.EqualTo(0.5f).Within(0.001f));

            hitChain.RecordHit();
            Assert.That(hitChain.Score, Is.EqualTo(1f));
        }
    }
}
