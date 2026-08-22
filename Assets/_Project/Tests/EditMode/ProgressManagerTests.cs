using CreativeAI.Core.EventSystem;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 進行度の更新通知とフラグの読み書きの検証。
    /// </summary>
    public class ProgressManagerTests
    {
        private GameObject _go;
        private ProgressManager _pm;

        [SetUp]
        public void SetUp()
        {
            // EditMode では Awake は走らないため、Instance/DontDestroyOnLoad には依存しない。
            _go = new GameObject(nameof(ProgressManager));
            _pm = _go.AddComponent<ProgressManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void AdvanceTo_NewValue_RaisesOnProgressChanged()
        {
            int calls = 0;
            _pm.OnProgressChanged += () => calls++;

            _pm.AdvanceTo(1);

            Assert.AreEqual(1, _pm.Progress);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void AdvanceTo_SameValue_DoesNotRaise()
        {
            _pm.AdvanceTo(3);

            int calls = 0;
            _pm.OnProgressChanged += () => calls++;
            _pm.AdvanceTo(3);

            Assert.AreEqual(3, _pm.Progress);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void GetFlag_UnsetKey_ReturnsEmpty_ThenReturnsSetValue()
        {
            Assert.AreEqual(string.Empty, _pm.GetFlag("girl_choice"));

            _pm.SetFlag("girl_choice", "together");

            Assert.AreEqual("together", _pm.GetFlag("girl_choice"));
        }
    }
}
