using System.Collections;
using CreativeAI.Gameplay;
using CreativeAI.Session;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// タイトルから常駐一式(進行・モード・インベントリ・UI・プレイヤー)を組み立てる流れの検証。
    /// Awake で立つ Instance(二重生成ガード)に依存するため PlayMode で回す。
    /// </summary>
    public class SessionResidencyPlayModeTests
    {
        private GameObject _sceneControllerGo;

        [SetUp]
        public void SetUp()
        {
            _sceneControllerGo = new GameObject("PersistentSystems");
            _sceneControllerGo.AddComponent<SceneController>();
        }

        [UnityTearDown]
        public IEnumerator TearDownRoutine()
        {
            DestroyResident<ProgressManager>();
            DestroyResident<GameModeManager>();
            DestroyResident<InventoryManager>();
            DestroyResident<EventPlayer>();
            DestroyResident<UIRoot>();
            Object.Destroy(_sceneControllerGo);
            yield return null; // Destroy の反映を待つ(次のテストへ持ち越さない)
        }

        private static void DestroyResident<T>()
            where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude))
                Object.Destroy(found.gameObject);
        }

        private static bool EnsureSession() => GameStarter.EnsureSession(out _);

        private static int CountOf<T>()
            where T : MonoBehaviour =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Exclude).Length;

        [UnityTest]
        public IEnumerator EnsureSession_IsIdempotent()
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(EnsureSession());
                yield return null;
            }

            Assert.AreEqual(1, CountOf<ProgressManager>(), "連打・再入場で二重生成しない");
            Assert.AreEqual(1, CountOf<GameModeManager>());
            Assert.AreEqual(1, CountOf<InventoryManager>());
            Assert.AreEqual(1, CountOf<EventPlayer>());
        }

        [UnityTest]
        public IEnumerator EnsureResidents_PublishesManagersThroughInstance()
        {
            // プレイヤーは GameModeManager を購読するので、先に立っている必要がある。
            GameSession.EnsureResidents(null);
            yield return null; // Awake で Instance が立つ

            Assert.IsNotNull(GameModeManager.Instance, "モードの単一ソース");
            Assert.IsNotNull(ProgressManager.Instance, "進行度の単一ソース");
            Assert.IsNotNull(EventPlayerService.Current, "EventTrigger が使う会話再生の seam");
        }

        [UnityTest]
        public IEnumerator EnsureResidents_DoubleCall_KeepsASingleInstance()
        {
            GameSession.EnsureResidents(null);
            yield return null;
            var progress = ProgressManager.Instance;

            GameSession.EnsureResidents(null);
            yield return null;

            Assert.AreSame(progress, ProgressManager.Instance, "同じ実体を使い回す");
            Assert.AreEqual(1, CountOf<ProgressManager>());
        }

        [UnityTest]
        public IEnumerator ReturnToTitle_LoadsTitle_AndRemovesThePlayerAndCamera()
        {
            Assert.IsTrue(GameStarter.EnsureSession(out var config));
            var cameraRig = GameSession.EnsureCameraRig(config.cameraRigPrefab);
            var player = GameSession.EnsurePlayerRig(config.playerRigPrefab);
            yield return null;

            TitleReturnService.Current.ReturnToTitle();
            yield return PlayModeWait.Until(
                () => player == null && cameraRig == null,
                "倒れたプレイヤーとカメラを次の開始へ持ち越さない"
            );

            Assert.AreEqual(
                "Title",
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
            Assert.IsNull(PlayerRegistry.Current);
        }

        [UnityTest]
        public IEnumerator Residents_SurviveASceneLoad()
        {
            // セッション常駐はフィールド間のエリア遷移をまたぐ(DontDestroyOnLoad)。
            GameSession.EnsureResidents(null);
            InventoryManager.EnsureResident();
            yield return null;
            var progress = ProgressManager.Instance;
            var inventory = InventoryManager.Instance;

            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Title");

            Assert.AreSame(progress, ProgressManager.Instance, "進行度はシーンをまたいで生き残る");
            Assert.AreSame(inventory, InventoryManager.Instance, "所持品もまたぐ");
        }
    }
}
