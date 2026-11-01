using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 焦り度の交戦相手(警戒中の敵を、離れすぎるか警戒を解くまで保持する)の選び方の検証。
    /// </summary>
    public class PanicEngagementTrackerTests
    {
        private readonly List<GameObject> _spawned = new();
        private PanicEngagementTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new PanicEngagementTracker();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private (EnemyBaseController Enemy, Collider Collider) SpawnEnemy(
            Vector3 position,
            bool alerted = true
        )
        {
            var go = new GameObject("Enemy") { transform = { position = position } };
            _spawned.Add(go);
            var collider = go.AddComponent<BoxCollider>();
            var status = go.AddComponent<EnemyStatus>();
            var enemy = go.AddComponent<EnemyBaseController>();
            // EditMode では Awake が走らないので、Status を手動で結び付ける。
            TestReflection.SetField(enemy, "<Status>k__BackingField", status);
            enemy.IsAlerted = alerted;
            return (enemy, collider);
        }

        private static void Kill(EnemyBaseController enemy) =>
            TestReflection.SetField(enemy.Status, "<IsDead>k__BackingField", true);

        [Test]
        public void NearestAlertedEnemy_BecomesEngaged()
        {
            var (enemy, collider) = SpawnEnemy(new Vector3(5f, 0f, 0f));

            _tracker.Refresh(collider, Vector3.zero);

            Assert.AreSame(enemy, _tracker.EngagedEnemy);
        }

        [Test]
        public void NearestEnemyNotAlerted_IsNotEngaged()
        {
            var (_, collider) = SpawnEnemy(new Vector3(5f, 0f, 0f), alerted: false);

            _tracker.Refresh(collider, Vector3.zero);

            Assert.IsNull(_tracker.EngagedEnemy);
        }

        [Test]
        public void NearestDeadEnemy_IsNotEngaged()
        {
            var (enemy, collider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            Kill(enemy);

            _tracker.Refresh(collider, Vector3.zero);

            Assert.IsNull(_tracker.EngagedEnemy);
        }

        [Test]
        public void NoNearestEnemyAndNoPreviousEngagement_StaysNull()
        {
            _tracker.Refresh(null, Vector3.zero);

            Assert.IsNull(_tracker.EngagedEnemy);
        }

        [Test]
        public void ColliderWithoutEnemyController_IsNotEngaged()
        {
            var go = new GameObject("Prop");
            _spawned.Add(go);
            var collider = go.AddComponent<BoxCollider>();

            _tracker.Refresh(collider, Vector3.zero);

            Assert.IsNull(_tracker.EngagedEnemy);
        }

        [Test]
        public void EngagedEnemy_IsKeptAfterLeavingDetectionRange_WhileWithinEngagementRadius()
        {
            var (enemy, collider) = SpawnEnemy(new Vector3(25f, 0f, 0f));
            _tracker.Refresh(collider, Vector3.zero);

            _tracker.Refresh(null, Vector3.zero);

            Assert.AreSame(enemy, _tracker.EngagedEnemy);
        }

        [Test]
        public void EngagedEnemy_IsDropped_WhenBeyondEngagementRadius()
        {
            var (_, collider) = SpawnEnemy(new Vector3(25f, 0f, 0f));
            _tracker.Refresh(collider, Vector3.zero);

            // 敵は動かさず、プレイヤーが交戦半径(30m)の外まで逃げる。
            _tracker.Refresh(null, new Vector3(-10f, 0f, 0f));

            Assert.IsNull(_tracker.EngagedEnemy);
        }

        [Test]
        public void EngagementRadius_IgnoresHeightDifference()
        {
            var (enemy, collider) = SpawnEnemy(new Vector3(10f, 100f, 0f));
            _tracker.Refresh(collider, Vector3.zero);

            _tracker.Refresh(null, Vector3.zero);

            Assert.AreSame(enemy, _tracker.EngagedEnemy);
        }

        [Test]
        public void EngagedEnemy_IsDropped_WhenItLosesAlertOrDies()
        {
            var (calm, calmCollider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            _tracker.Refresh(calmCollider, Vector3.zero);
            calm.IsAlerted = false;

            _tracker.Refresh(null, Vector3.zero);
            Assert.IsNull(_tracker.EngagedEnemy, "警戒を解いたら外す");

            var (victim, victimCollider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            _tracker.Refresh(victimCollider, Vector3.zero);
            Kill(victim);

            _tracker.Refresh(null, Vector3.zero);
            Assert.IsNull(_tracker.EngagedEnemy, "倒したら外す");
        }

        [Test]
        public void SameColliderRefreshedAgain_ReflectsCurrentAlertState()
        {
            var (enemy, collider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            _tracker.Refresh(collider, Vector3.zero);
            enemy.IsAlerted = false;

            _tracker.Refresh(collider, Vector3.zero);

            Assert.IsNull(
                _tracker.EngagedEnemy,
                "敵の引き直しをキャッシュしても警戒状態は毎回見る"
            );
        }

        [Test]
        public void NewAlertedNearestEnemy_ReplacesCurrentEngagement()
        {
            var (first, firstCollider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            var (second, secondCollider) = SpawnEnemy(new Vector3(0f, 0f, 8f));
            _tracker.Refresh(firstCollider, Vector3.zero);
            Assert.AreSame(first, _tracker.EngagedEnemy);

            _tracker.Refresh(secondCollider, Vector3.zero);

            Assert.AreSame(second, _tracker.EngagedEnemy);
        }

        [Test]
        public void Clear_RemovesEngagement()
        {
            var (_, collider) = SpawnEnemy(new Vector3(5f, 0f, 0f));
            _tracker.Refresh(collider, Vector3.zero);

            _tracker.Clear();

            Assert.IsNull(_tracker.EngagedEnemy);
        }
    }
}
