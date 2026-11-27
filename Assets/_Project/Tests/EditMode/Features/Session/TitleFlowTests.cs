using CreativeAI.Gameplay;
using CreativeAI.Session;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」で常駐一式を正しい順に生成するかの検証。
    /// 生成順はマネージャ → Inventory → UI(UIManager) → プレイヤー。
    /// シーンロードは SceneLoadPlayModeTests、二重生成しないことは SessionResidencyPlayModeTests 側。
    /// </summary>
    public class TitleFlowTests
    {
        private readonly TestResidents _residents = new();

        [SetUp]
        public void SetUp()
        {
            _residents.Add<SceneLoader>();
        }

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            DestroyResident<ProgressManager>();
            DestroyResident<GameModeManager>();
            DestroyResident<InventoryManager>();
            DestroyResident<UIManager>();
        }

        private static void DestroyResident<T>()
            where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude))
                Object.DestroyImmediate(found.gameObject);
            TestReflection.SetStaticProperty<T>("Instance", null);
        }

        private static bool EnsureSession() => GameStarter.EnsureSession(out _);

        private static int CountOf<T>()
            where T : MonoBehaviour =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Length;

        [Test]
        public void ResetForNewGame_ClearsProgressAndFlagsFromThePreviousPlay()
        {
            Assert.IsTrue(EnsureSession());
            // EditMode では Awake が走らず Instance が入らないので、生成された進行度管理を直接入れる。
            TestReflection.SetStaticProperty(
                "Instance",
                Object.FindAnyObjectByType<ProgressManager>()
            );
            ProgressManager.Instance.LoadState(
                5,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["hero_choice"] = "same",
                }
            );

            GameStarter.ResetForNewGame();

            Assert.AreEqual(0, ProgressManager.Instance.Progress);
            Assert.AreEqual(string.Empty, ProgressManager.Instance.GetFlag("hero_choice"));
        }

        [Test]
        public void EnsureSession_MarksTheSessionAsStartedFromTitle()
        {
            Assert.IsTrue(EnsureSession());

            Assert.IsTrue(GameSession.StartedFromTitle);
        }

        [Test]
        public void EnsureSession_CreatesEveryResidentSystem()
        {
            Assert.IsTrue(EnsureSession());

            Assert.AreEqual(1, CountOf<ProgressManager>(), "① 進行度");
            Assert.AreEqual(1, CountOf<GameModeManager>(), "① モード");
            Assert.AreEqual(1, CountOf<InventoryManager>(), "② 所持品");
        }

        [Test]
        public void EnsureSession_WithoutSceneLoader_FailsWithoutCreatingAnything()
        {
            TestReflection.SetStaticProperty<SceneLoader>("Instance", null);
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("SceneLoader.Instance is null")
            );

            Assert.IsFalse(EnsureSession());

            Assert.AreEqual(0, CountOf<ProgressManager>(), "遷移できないなら常駐も作らない");
            Assert.AreEqual(0, CountOf<InventoryManager>());
        }

        // --- プレイヤーリグ ---

        [Test]
        public void EnsurePlayer_DoesNotSpawnASecondPlayer()
        {
            var prefab = new GameObject("PlayerRig") { tag = "Player" };
            try
            {
                var first = GameSession.EnsurePlayerRig(prefab);
                var second = GameSession.EnsurePlayerRig(prefab);

                Assert.IsNotNull(first);
                Assert.AreSame(
                    first,
                    second,
                    "既に Player タグが居れば作らない(連打・タイトル復帰)"
                );
            }
            finally
            {
                foreach (
                    var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude)
                )
                    if (go != null && go.CompareTag("Player"))
                        Object.DestroyImmediate(go);
            }
        }
    }
}
