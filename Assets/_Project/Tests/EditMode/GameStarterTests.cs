using System.Collections.Generic;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// GameStarter のプレイヤー生成骨格の検証。EditMode では Application.isPlaying=false のため
    /// DontDestroyOnLoad は呼ばれない(GameStarter 側でガード済み)。
    /// </summary>
    public class GameStarterTests
    {
        private readonly List<GameObject> _cleanup = new();

        private GameStarter MakeStarter()
        {
            var go = new GameObject(nameof(GameStarter));
            _cleanup.Add(go);
            return go.AddComponent<GameStarter>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _cleanup)
                if (go != null)
                    Object.DestroyImmediate(go);
            _cleanup.Clear();
        }

        [Test]
        public void EnsurePlayer_NullPrefab_ReturnsNull_NoThrow()
        {
            var starter = MakeStarter();

            GameObject result = null;
            Assert.DoesNotThrow(() => result = starter.EnsurePlayer());
            Assert.IsNull(result);
        }

        [Test]
        public void EnsurePlayer_WithPrefab_SpawnsInstance()
        {
            var starter = MakeStarter();
            var prefab = new GameObject("PlayerRig");
            _cleanup.Add(prefab);
            SetPrivate(starter, "_playerRigPrefab", prefab);

            var spawned = starter.EnsurePlayer();

            Assert.IsNotNull(spawned);
            Assert.AreNotSame(prefab, spawned); // テンプレでなく実体が生成される
            Assert.AreEqual("PlayerRig", spawned.name); // "(Clone)" が付かない
            _cleanup.Add(spawned);
        }

        // Inspector 用の private [SerializeField] をテストから設定する。
        private static void SetPrivate(Object target, string field, Object value)
        {
            var so = new UnityEditor.SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
