using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// シーン遷移の多重ロードを防ぐ処理の検証(フィールドシーンは常に1つだけロードする)。
    /// ロード進行そのもの(オーバーレイのフェード・LoadSceneAsync)は PlayMode でないと回せないため、
    /// ここでは「ロード中に来た2本目を無視する」ガードだけを検証する。
    /// </summary>
    public class SceneLoaderTests
    {
        private GameObject _go;
        private SceneLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(SceneLoader));
            _loader = _go.AddComponent<SceneLoader>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void LoadScene_WhileAlreadyLoading_IsIgnored()
        {
            TestReflection.SetField(_loader, "_isLoading", true);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("already loading")
            );

            _loader.LoadScene("Field_Area03");

            // ガードが効いていれば StartCoroutine まで到達せず、状態も変わらない。
            Assert.IsTrue(TestReflection.GetField<bool>(_loader, "_isLoading"));
        }

        [Test]
        public void LoadScene_WhileAlreadyLoading_IgnoresEveryExtraCall()
        {
            TestReflection.SetField(_loader, "_isLoading", true);
            for (int i = 0; i < 3; i++)
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("already loading")
                );
                _loader.LoadScene($"Field_Area0{i}");
            }

            Assert.IsTrue(TestReflection.GetField<bool>(_loader, "_isLoading"));
        }

        [Test]
        public void NotLoading_ByDefault()
        {
            Assert.IsFalse(TestReflection.GetField<bool>(_loader, "_isLoading"));
        }
    }
}
