using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// シーン遷移の進行順の検証(暗幕 → ロード → 到着処理 → 暗幕解除)。
    /// 「ロード画面はシーンではなく UI オーバーレイ」(documents/Specification.md §3)。
    /// コルーチンと実ロードが要るので PlayMode で回す。
    /// </summary>
    public class SceneLoadPlayModeTests
    {
        /// <summary>呼ばれた順番だけを記録するオーバーレイ。実際のフェードはしない。</summary>
        private sealed class SpyOverlay : MonoBehaviour, ILoadingOverlay
        {
            public readonly List<string> Calls = new();
            public readonly List<float> Progress = new();

            public IEnumerator ShowCoroutine(float seconds)
            {
                Calls.Add("Show");
                yield break;
            }

            public IEnumerator HideCoroutine(float seconds)
            {
                Calls.Add("Hide");
                yield break;
            }

            public void SetProgress(float value) => Progress.Add(value);
        }

        private GameObject _go;
        private SceneController _controller;
        private SpyOverlay _overlay;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SceneController");
            var overlayGo = new GameObject("Overlay");
            overlayGo.transform.SetParent(_go.transform);
            _overlay = overlayGo.AddComponent<SpyOverlay>();

            // Awake が GetComponentInChildren<ILoadingOverlay> で拾えるよう、子に置いてから足す。
            _controller = _go.AddComponent<SceneController>();
            SetPrivate(_controller, "_minDisplaySeconds", 0f);
            SetPrivate(_controller, "_fadeSeconds", 0f);
        }

        [UnityTearDown]
        public IEnumerator TearDownRoutine()
        {
            if (_go != null)
                Object.Destroy(_go);
            // Destroy は遅延するので1フレーム待つ。待たないと次の SetUp で作った
            // SceneController の Awake が「まだ生きている Instance」を見て自滅する。
            yield return null;
        }

        private static void SetPrivate(object target, string name, object value) =>
            target
                .GetType()
                .GetField(
                    name,
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )
                .SetValue(target, value);

        private static T GetPrivate<T>(object target, string name) =>
            (T)
                target
                    .GetType()
                    .GetField(
                        name,
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .GetValue(target);

        [UnityTest]
        public IEnumerator LoadScene_RunsOverlayThenSceneThenArrivalCallback()
        {
            bool arrived = false;
            string sceneAtArrival = null;

            _controller.LoadScene(
                "01_Title",
                onSceneActivated: () =>
                {
                    arrived = true;
                    sceneAtArrival = SceneManager.GetActiveScene().name;
                }
            );

            float timeout = Time.realtimeSinceStartup + 20f;
            while (GetPrivate<bool>(_controller, "_isLoading"))
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "ロードが終わらない");
                yield return null;
            }

            CollectionAssert.AreEqual(
                new[] { "Show", "Hide" },
                _overlay.Calls,
                "暗幕を出してからロードし、最後に解除する"
            );
            Assert.IsTrue(arrived, "到着コールバックが呼ばれる");
            Assert.AreEqual(
                "01_Title",
                sceneAtArrival,
                "到着処理はシーン有効化の後(暗幕の下)で走る"
            );
        }

        [UnityTest]
        public IEnumerator LoadScene_ReportsProgressUpTo100Percent()
        {
            _controller.LoadScene("01_Title");

            float timeout = Time.realtimeSinceStartup + 20f;
            while (GetPrivate<bool>(_controller, "_isLoading"))
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "ロードが終わらない");
                yield return null;
            }

            Assert.Greater(_overlay.Progress.Count, 0, "進捗が1回以上通知される");
            Assert.AreEqual(1f, _overlay.Progress[^1], 1e-3f, "最後は 100%");
            foreach (float p in _overlay.Progress)
                Assert.That(p, Is.InRange(0f, 1f), "進捗は 0..1 に正規化される");
        }

        [UnityTest]
        public IEnumerator LoadScene_LoadsExactlyOneScene()
        {
            // §3「常に1つだけロードされ互いに相互排他」。Single ロードなので加算されない。
            _controller.LoadScene("01_Title");

            float timeout = Time.realtimeSinceStartup + 20f;
            while (GetPrivate<bool>(_controller, "_isLoading"))
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "ロードが終わらない");
                yield return null;
            }

            Assert.AreEqual(1, SceneManager.sceneCount, "フィールド/タイトルは相互排他で1つだけ");
        }

        [UnityTest]
        public IEnumerator LoadScene_WhileLoading_IsIgnored()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("already loading")
            );

            _controller.LoadScene("01_Title");
            _controller.LoadScene("01_Title"); // ロード中の2本目

            float timeout = Time.realtimeSinceStartup + 20f;
            while (GetPrivate<bool>(_controller, "_isLoading"))
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "ロードが終わらない");
                yield return null;
            }

            CollectionAssert.AreEqual(
                new[] { "Show", "Hide" },
                _overlay.Calls,
                "2本目が走っていれば Show が2回出る"
            );
        }
    }
}
