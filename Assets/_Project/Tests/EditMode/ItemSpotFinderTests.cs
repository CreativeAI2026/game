using System.Collections.Generic;
using CreativeAI.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// アイテムの置き場所の補正の検証(床に合わせる / 小物や壁と重なるなら空いた場所へずらす)。
    /// NavMesh は使わず、コライダーだけで判定できる範囲を見る。
    /// </summary>
    public class ItemSpotFinderTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null)
                    Object.DestroyImmediate(go);
            _created.Clear();
        }

        private void MakeBox(Vector3 center, Vector3 size, bool trigger = false)
        {
            var go = new GameObject("Box", typeof(BoxCollider));
            go.transform.position = center;
            var box = go.GetComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            _created.Add(go);
        }

        /// <summary>y=0 が天面の広い床。</summary>
        private void MakeFloor() => MakeBox(new Vector3(0f, -0.5f, 0f), new Vector3(50f, 1f, 50f));

        [Test]
        public void Floating_Candidate_IsPlacedOnTheFloor()
        {
            MakeFloor();

            Assert.IsTrue(ItemSpotFinder.TryFind(new Vector3(1f, 0.15f, 1f), null, out var spot, requireNavMesh: false));

            Assert.AreEqual(ItemSpotFinder.LiftAboveFloor, spot.y, 0.01f);
            Assert.AreEqual(1f, spot.x, 0.01f);
            Assert.AreEqual(1f, spot.z, 0.01f);
        }

        [Test]
        public void Candidate_InsideAProp_IsMovedOutOfIt()
        {
            MakeFloor();
            MakeBox(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f)); // 小物(机など)

            Assert.IsTrue(ItemSpotFinder.TryFind(new Vector3(0f, 0.1f, 0f), null, out var spot, requireNavMesh: false));

            Assert.IsFalse(ItemSpotFinder.IsBlocked(spot), "ずらした先が小物と重なっています。");
            Assert.That(
                new Vector2(spot.x, spot.z).magnitude,
                Is.GreaterThan(1f),
                "小物の外へ出ていません。"
            );
        }

        [Test]
        public void Triggers_AreNotTreatedAsObstacles()
        {
            MakeFloor();
            MakeBox(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 2f), trigger: true); // イベントの判定など

            Assert.IsTrue(ItemSpotFinder.TryFind(new Vector3(0f, 0f, 0f), null, out var spot, requireNavMesh: false));

            Assert.AreEqual(0f, spot.x, 0.01f);
            Assert.AreEqual(0f, spot.z, 0.01f);
        }

        [Test]
        public void Candidate_TooCloseToAnotherItem_IsMoved()
        {
            MakeFloor();
            var first = new Vector3(0f, ItemSpotFinder.LiftAboveFloor, 0f);

            Assert.IsTrue(
                ItemSpotFinder.TryFind(new Vector3(0.1f, 0f, 0f), new[] { first }, out var spot, requireNavMesh: false)
            );

            Assert.That(
                Vector3.Distance(first, spot),
                Is.GreaterThanOrEqualTo(ItemSpotFinder.MinSeparation)
            );
        }

        [Test]
        public void SurroundedByObstacles_ReturnsFalse()
        {
            MakeFloor();
            MakeBox(new Vector3(0f, 2f, 0f), new Vector3(20f, 4f, 20f)); // 周囲3m以内が全部壁

            Assert.IsFalse(ItemSpotFinder.TryFind(new Vector3(0f, 0.1f, 0f), null, out _, requireNavMesh: false));
        }
    }
}
