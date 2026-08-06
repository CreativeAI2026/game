using CreativeAI.Core.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// シーン遷移役の多重ロードガード(documents/Specification.md §3: フィールドシーンは常に1つだけロード)。
    /// ロード進行そのもの(オーバーレイのフェード・LoadSceneAsync)は PlayMode でないと回せないため、
    /// ここでは「ロード中に来た2本目を無視する」ガードだけを検証する。
    /// </summary>
    public class SceneControllerTests
    {
        private GameObject _go;
        private SceneController _controller;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(SceneController));
            _controller = _go.AddComponent<SceneController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void LoadScene_WhileAlreadyLoading_IsIgnored()
        {
            TestReflection.SetField(_controller, "_isLoading", true);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("already loading")
            );

            _controller.LoadScene("Field_Area03");

            // ガードが効いていれば StartCoroutine まで到達せず、状態も変わらない。
            Assert.IsTrue(TestReflection.GetField<bool>(_controller, "_isLoading"));
        }

        [Test]
        public void LoadScene_WhileAlreadyLoading_IgnoresEveryExtraCall()
        {
            TestReflection.SetField(_controller, "_isLoading", true);
            for (int i = 0; i < 3; i++)
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("already loading")
                );
                _controller.LoadScene($"Field_Area0{i}");
            }

            Assert.IsTrue(TestReflection.GetField<bool>(_controller, "_isLoading"));
        }

        [Test]
        public void NotLoading_ByDefault()
        {
            Assert.IsFalse(TestReflection.GetField<bool>(_controller, "_isLoading"));
        }
    }
}
