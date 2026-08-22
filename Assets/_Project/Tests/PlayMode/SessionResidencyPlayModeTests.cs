using System.Collections;
using System.Reflection;
using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.SceneManagement;
using CreativeAI.Gameplay;
using CreativeAI.UI.TitleUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// タイトルから常駐一式(進行・モード・インベントリ・UI・プレイヤー)を組み立てる流れの検証。
    /// 対象は documents/Specification.md §6, §6.1 のうち Awake で立つ Instance に依存する部分。
    /// EditMode では Awake が走らず
    /// 二重生成ガードが効かないため、ここは PlayMode で回す。
    /// </summary>
    public class SessionResidencyPlayModeTests
    {
        private GameObject _titleGo;
        private TitleUIController _title;
        private GameObject _sceneControllerGo;
        private GameObject _starterGo;

        [SetUp]
        public void SetUp()
        {
            _sceneControllerGo = new GameObject("PersistentSystems");
            _sceneControllerGo.AddComponent<SceneController>();

            _starterGo = new GameObject("GameStarter");
            var starter = _starterGo.AddComponent<GameStarter>();

            // Awake が _tapToStartButton を要求するので、非アクティブで組んでから起こす。
            _titleGo = new GameObject("TitleUI");
            _titleGo.SetActive(false);
            _title = _titleGo.AddComponent<TitleUIController>();
            var button = _titleGo.AddComponent<Button>();
            SetPrivate(_title, "_tapToStartButton", button);
            SetPrivate(_title, "_gameStarter", starter);
            _titleGo.SetActive(true);
        }

        [UnityTearDown]
        public IEnumerator TearDownRoutine()
        {
            BattleRunnerService.Current = null;
            DestroyResident<ProgressManager>();
            DestroyResident<GameModeManager>();
            DestroyResident<InventoryManager>();
            DestroyResident<RecipeBookManager>();
            DestroyResident<EventPlayer>();
            Object.Destroy(_titleGo);
            Object.Destroy(_starterGo);
            Object.Destroy(_sceneControllerGo);
            yield return null; // Destroy の反映を待つ(次のテストへ持ち越さない)
        }

        private static void DestroyResident<T>()
            where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.Destroy(found.gameObject);
        }

        private static void SetPrivate(object target, string name, object value) =>
            target
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private bool EnsureSessionAndPlayer() =>
            (bool)
                _title
                    .GetType()
                    .GetMethod(
                        "EnsureSessionAndPlayer",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    .Invoke(_title, null);

        private static int CountOf<T>()
            where T : MonoBehaviour => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator EnsureSessionAndPlayer_IsIdempotent()
        {
            // PlayerRig Prefab 未割当なのでプレイヤー生成だけは警告してスキップされる(呼ぶたび1回)。
            for (int i = 0; i < 2; i++)
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("playerRigPrefab が未割当")
                );
                Assert.IsTrue(EnsureSessionAndPlayer());
                yield return null;
            }

            Assert.AreEqual(1, CountOf<ProgressManager>(), "連打・再入場で二重生成しない");
            Assert.AreEqual(1, CountOf<GameModeManager>());
            Assert.AreEqual(1, CountOf<InventoryManager>());
            Assert.AreEqual(1, CountOf<RecipeBookManager>());
            Assert.AreEqual(1, CountOf<EventPlayer>());
        }

        [UnityTest]
        public IEnumerator SessionBootstrap_PublishesManagersThroughInstance()
        {
            // spec §6.1: プレイヤーは GameModeManager を購読するので、先に立っている必要がある。
            SessionBootstrap.EnsureSession();
            yield return null; // Awake で Instance が立つ

            Assert.IsNotNull(GameModeManager.Instance, "モードの単一ソース");
            Assert.IsNotNull(ProgressManager.Instance, "進行度の単一ソース");
            Assert.IsNotNull(EventPlayerService.Current, "EventTrigger が使う会話再生の seam");
        }

        [UnityTest]
        public IEnumerator SessionBootstrap_DoubleCall_KeepsASingleInstance()
        {
            SessionBootstrap.EnsureSession();
            yield return null;
            var progress = ProgressManager.Instance;

            SessionBootstrap.EnsureSession();
            yield return null;

            Assert.AreSame(progress, ProgressManager.Instance, "同じ実体を使い回す");
            Assert.AreEqual(1, CountOf<ProgressManager>());
        }

        [UnityTest]
        public IEnumerator Residents_SurviveASceneLoad()
        {
            // §6: セッション常駐はフィールド間のエリア遷移をまたぐ(DontDestroyOnLoad)。
            SessionBootstrap.EnsureSession();
            InventoryManager.EnsureResident();
            yield return null;
            var progress = ProgressManager.Instance;
            var inventory = InventoryManager.Instance;

            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("01_Title");

            Assert.AreSame(progress, ProgressManager.Instance, "進行度はシーンをまたいで生き残る");
            Assert.AreSame(inventory, InventoryManager.Instance, "所持品もまたぐ");
        }
    }
}
