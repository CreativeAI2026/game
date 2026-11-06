using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// シーン遷移の進行順の検証(暗幕 → ロード → 到着処理 → 暗幕解除)。
    /// 「ロード画面はシーンではなく UI オーバーレイ」。
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
            TestReflection.SetField(_controller, "_minDisplaySeconds", 0f);
            TestReflection.SetField(_controller, "_fadeSeconds", 0f);
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

        [UnityTest]
        public IEnumerator LoadScene_RunsOverlayThenSceneThenArrivalCallback()
        {
            bool arrived = false;
            string sceneAtArrival = null;

            _controller.LoadScene(
                "Title",
                onSceneActivated: () =>
                {
                    arrived = true;
                    sceneAtArrival = SceneManager.GetActiveScene().name;
                }
            );

            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            CollectionAssert.AreEqual(
                new[] { "Show", "Hide" },
                _overlay.Calls,
                "暗幕を出してからロードし、最後に解除する"
            );
            Assert.IsTrue(arrived, "到着コールバックが呼ばれる");
            Assert.AreEqual("Title", sceneAtArrival, "到着処理はシーン有効化の後(暗幕の下)で走る");
        }

        [UnityTest]
        public IEnumerator LoadScene_ReportsProgressUpTo100Percent()
        {
            _controller.LoadScene("Title");

            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            Assert.Greater(_overlay.Progress.Count, 0, "進捗が1回以上通知される");
            Assert.AreEqual(1f, _overlay.Progress[^1], 1e-3f, "最後は 100%");
            foreach (float p in _overlay.Progress)
                Assert.That(p, Is.InRange(0f, 1f), "進捗は 0..1 に正規化される");
        }

        /// <summary>
        /// 本体の有効化を待つ。前のテストで Title が開いたままのことがあるので、シーン名ではなく読み込みの通知で待つ。
        /// </summary>
        private static IEnumerator WaitForSceneLoaded()
        {
            bool loaded = false;
            void OnLoaded(Scene scene, LoadSceneMode mode) => loaded = true;
            SceneManager.sceneLoaded += OnLoaded;
            try
            {
                yield return PlayModeWait.Until(() => loaded, "本体が有効化される", 20f);
            }
            finally
            {
                SceneManager.sceneLoaded -= OnLoaded;
            }
        }

        [UnityTest]
        public IEnumerator LoadScene_WaitsForAdditiveLoadsBeforeArrivalCallback()
        {
            // 小物シーンの代わりに、手で終わらせる読み込みを登録する(LoadScene の直後なら Clear のあと)。
            float additive = 0f;
            bool arrived = false;
            _controller.LoadScene("Title", onSceneActivated: () => arrived = true);
            PendingSceneLoads.Register(() => additive);

            yield return WaitForSceneLoaded();
            yield return PlayModeWait.Frames();
            Assert.IsFalse(arrived, "重ねる読み込みが終わるまで到着処理は呼ばれない");
            CollectionAssert.AreEqual(new[] { "Show" }, _overlay.Calls, "暗転はまだ明けない");

            additive = 1f;
            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            Assert.IsTrue(arrived, "重ねる読み込みが終わったら到着処理が呼ばれる");
            CollectionAssert.AreEqual(new[] { "Show", "Hide" }, _overlay.Calls);
            Assert.AreEqual(0, PendingSceneLoads.Count, "待ち終えた読み込みは持ち越さない");
        }

        [UnityTest]
        public IEnumerator LoadScene_ProgressIncludesAdditiveLoadsAndNeverGoesBack()
        {
            float additive = 0f;
            _controller.LoadScene("Title");
            PendingSceneLoads.Register(() => additive);

            yield return WaitForSceneLoaded();
            additive = 0.5f;
            yield return PlayModeWait.Frames();
            float share = TestReflection.GetField<float>(_controller, "_additiveLoadShare");
            Assert.AreEqual(
                1f - share * 0.5f,
                _overlay.Progress[^1],
                1e-3f,
                "本体の分のあとに、重ねる読み込みの分が割合どおり足される"
            );

            additive = 1f;
            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            Assert.AreEqual(1f, _overlay.Progress[^1], 1e-3f, "最後は 100%");
            for (int i = 1; i < _overlay.Progress.Count; i++)
                Assert.GreaterOrEqual(
                    _overlay.Progress[i],
                    _overlay.Progress[i - 1],
                    "プログレスバーは戻らない"
                );
        }

        [UnityTest]
        public IEnumerator LoadScene_LoadsExactlyOneScene()
        {
            // 「常に1つだけロードされ互いに相互排他」。Single ロードなので加算されない。
            _controller.LoadScene("Title");

            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            Assert.AreEqual(1, SceneManager.sceneCount, "フィールド/タイトルは相互排他で1つだけ");
        }

        [UnityTest]
        public IEnumerator LoadScene_WhileLoading_IsIgnored()
        {
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("already loading")
            );

            _controller.LoadScene("Title");
            _controller.LoadScene("Title"); // ロード中の2本目

            yield return PlayModeWait.Until(
                () => !TestReflection.GetField<bool>(_controller, "_isLoading"),
                "ロードが終わる",
                20f
            );

            CollectionAssert.AreEqual(
                new[] { "Show", "Hide" },
                _overlay.Calls,
                "2本目が走っていれば Show が2回出る"
            );
        }
    }
}
