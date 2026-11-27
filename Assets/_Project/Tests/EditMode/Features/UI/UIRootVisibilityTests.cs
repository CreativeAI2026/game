using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// UIRoot の表示切り替え(タイトルの暗転が降りる前に HUD が見えないようにする隠し・戻し)の検証。
    /// </summary>
    public sealed class UIRootVisibilityTests
    {
        private GameObject _go;
        private UIRoot _root;
        private Canvas _hud;
        private Canvas _alreadyOff;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("UIRoot");
            _root = _go.AddComponent<UIRoot>();

            _hud = new GameObject("Hud").AddComponent<Canvas>();
            _hud.transform.SetParent(_go.transform);

            // 元から出していない Canvas(閉じているパネルなど)。戻すときに勝手に出してはいけない。
            _alreadyOff = new GameObject("Panel").AddComponent<Canvas>();
            _alreadyOff.transform.SetParent(_go.transform);
            _alreadyOff.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void SetVisible_False_HidesTheCanvases()
        {
            _root.SetVisible(false);

            Assert.IsFalse(_hud.enabled);
        }

        [Test]
        public void SetVisible_True_RestoresOnlyWhatWasHidden()
        {
            _root.SetVisible(false);

            _root.SetVisible(true);

            Assert.IsTrue(_hud.enabled);
            Assert.IsFalse(_alreadyOff.enabled);
        }

        [Test]
        public void SetVisible_HiddenTwice_StillRestoresOnTrue()
        {
            _root.SetVisible(false);
            _root.SetVisible(false);

            _root.SetVisible(true);

            Assert.IsTrue(_hud.enabled);
        }
    }
}
