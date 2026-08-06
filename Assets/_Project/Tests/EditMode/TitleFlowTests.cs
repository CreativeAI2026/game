using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.SceneManagement;
using CreativeAI.Gameplay;
using CreativeAI.UI.TitleUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」で常駐一式を組み立てる流れ(documents/Specification.md §6, §6.1)。
    /// 生成順は マネージャ → Inventory → RecipeBook → UIRoot → プレイヤー。
    /// シーンロード自体はコルーチンなので、ここでは常駐の生成契約だけを検証する。
    /// 冪等性(Instance による二重生成ガード)は Awake が要るので TitleFlowPlayModeTests 側。
    /// </summary>
    public class TitleFlowTests
    {
        private GameObject _titleGo;
        private TitleUIController _title;
        private GameObject _sceneControllerGo;
        private GameObject _starterGo;
        private GameStarter _starter;

        [SetUp]
        public void SetUp()
        {
            _sceneControllerGo = new GameObject("PersistentSystems");
            var controller = _sceneControllerGo.AddComponent<SceneController>();
            TestReflection.SetStaticProperty("Instance", controller);

            _starterGo = new GameObject("GameStarter");
            _starter = _starterGo.AddComponent<GameStarter>();

            _titleGo = new GameObject("TitleUI");
            _title = _titleGo.AddComponent<TitleUIController>();
            var button = _titleGo.AddComponent<Button>();
            TestReflection.SetField(_title, "_tapToStartButton", button);
            TestReflection.SetField(_title, "_gameStarter", _starter);
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.SetStaticProperty<SceneController>("Instance", null);
            BattleRunnerService.Current = null;
            DestroyResident<ProgressManager>();
            DestroyResident<GameModeManager>();
            DestroyResident<InventoryManager>();
            DestroyResident<RecipeBookManager>();
            DestroyResident<EventPlayer>();
            Object.DestroyImmediate(_titleGo);
            Object.DestroyImmediate(_starterGo);
            Object.DestroyImmediate(_sceneControllerGo);
        }

        private static void DestroyResident<T>()
            where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(found.gameObject);
            TestReflection.SetStaticProperty<T>("Instance", null);
        }

        private bool EnsureSessionAndPlayer() =>
            (bool)TestReflection.Invoke(_title, "EnsureSessionAndPlayer");

        private static int CountOf<T>()
            where T : MonoBehaviour => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;

        [Test]
        public void EnsureSessionAndPlayer_CreatesEveryResidentSystem()
        {
            // PlayerRig Prefab 未割当なので、プレイヤー生成だけは警告してスキップされる。
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("playerRigPrefab が未割当")
            );

            Assert.IsTrue(EnsureSessionAndPlayer());

            Assert.AreEqual(1, CountOf<ProgressManager>(), "① 進行度");
            Assert.AreEqual(1, CountOf<GameModeManager>(), "① モード");
            Assert.AreEqual(1, CountOf<EventPlayer>(), "① 会話イベント指揮役");
            Assert.AreEqual(1, CountOf<InventoryManager>(), "② 所持品");
            Assert.AreEqual(1, CountOf<RecipeBookManager>(), "②' レシピ解禁");
            Assert.IsNotNull(BattleRunnerService.Current, "④ 戦闘実行の seam");
        }

        [Test]
        public void EnsureSessionAndPlayer_WithoutSceneController_FailsWithoutCreatingAnything()
        {
            TestReflection.SetStaticProperty<SceneController>("Instance", null);
            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("SceneController.Instance is null")
            );

            Assert.IsFalse(EnsureSessionAndPlayer());

            Assert.AreEqual(0, CountOf<ProgressManager>(), "遷移できないなら常駐も作らない");
            Assert.AreEqual(0, CountOf<InventoryManager>());
        }

        // --- GameStarter(⑤ プレイヤーリグ) ---

        [Test]
        public void EnsurePlayer_DoesNotSpawnASecondPlayer()
        {
            var prefab = new GameObject("PlayerRig") { tag = "Player" };
            try
            {
                TestReflection.SetField(_starter, "_playerRigPrefab", prefab);

                var first = _starter.EnsurePlayer();
                var second = _starter.EnsurePlayer();

                Assert.IsNotNull(first);
                Assert.AreSame(
                    first,
                    second,
                    "既に Player タグが居れば作らない(連打・タイトル復帰)"
                );
            }
            finally
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (go != null && go.CompareTag("Player"))
                        Object.DestroyImmediate(go);
            }
        }
    }
}
