using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// プレイヤーリグ生成の検証。EditMode では Application.isPlaying=false のため
    /// DontDestroyOnLoad は呼ばれない(GameSession 側でガード済み)。
    /// </summary>
    public class PlayerRigSpawnTests
    {
        private readonly List<GameObject> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _cleanup)
                if (go != null)
                    Object.DestroyImmediate(go);
            _cleanup.Clear();
        }

        [Test]
        public void EnsurePlayerRig_NullPrefab_ReturnsNull_NoThrow()
        {
            GameObject result = null;
            Assert.DoesNotThrow(() => result = GameSession.EnsurePlayerRig(null));
            Assert.IsNull(result);
        }

        [Test]
        public void EnsurePlayerRig_WithPrefab_SpawnsInstance()
        {
            var prefab = new GameObject("PlayerRig");
            _cleanup.Add(prefab);

            var spawned = GameSession.EnsurePlayerRig(prefab);

            Assert.IsNotNull(spawned);
            Assert.AreNotSame(prefab, spawned); // テンプレでなく実体が生成される
            Assert.AreEqual("PlayerRig", spawned.name); // "(Clone)" が付かない
            _cleanup.Add(spawned);
        }

        [Test]
        public void EnsureCameraRig_NullPrefab_ReturnsNull_NoThrow()
        {
            CameraRig result = null;
            Assert.DoesNotThrow(() => result = GameSession.EnsureCameraRig(null));
            Assert.IsNull(result);
        }

        [Test]
        public void EnsureCameraRig_SecondCall_ReturnsExisting()
        {
            // シーン上のテンプレだと Find がテンプレ自身を拾うので、本物の Prefab アセットを使う
            var prefab = Resources
                .Load<ResidentBootstrapConfig>(nameof(ResidentBootstrapConfig))
                .cameraRigPrefab;

            var first = GameSession.EnsureCameraRig(prefab);
            _cleanup.Add(first.gameObject);
            var second = GameSession.EnsureCameraRig(prefab);

            Assert.AreSame(first, second); // 2つ目は作らない
        }
    }
}
