using System.Reflection;
using CreativeAI.UI.InventoryUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class HoverScaleOnPointerSettingsTests
    {
        private GameObject _gameObject;
        private HoverScaleOnPointer _hover;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("HoverTarget", typeof(RectTransform));
            _hover = _gameObject.AddComponent<HoverScaleOnPointer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Defaults_KeepExistingScaleAndBounceEffectsEnabled()
        {
            Assert.IsTrue(_hover.HoverScaleEnabled);
            Assert.IsTrue(_hover.BounceEnabled);
        }

        [Test]
        public void HoverScaleDisabled_DoesNotCreateScaleTween()
        {
            _hover.SetHoverScaleEnabled(false);

            _hover.OnPointerEnter(null);

            Assert.IsNull(GetPrivateField("_currentTween"));
        }

        [Test]
        public void BounceDisabled_DoesNotCreateBounceTween()
        {
            _hover.SetBounceEnabled(false);

            _hover.AcquireLock();

            Assert.IsNull(GetPrivateField("_bounceTween"));
        }

        [Test]
        public void DisabledComponent_DoesNotForceScaleWhenHoverScaleIsOff()
        {
            _hover.SetHoverScaleEnabled(false);
            _gameObject.transform.localScale = Vector3.one * 2.5f;

            _hover.enabled = false;

            Assert.AreEqual(Vector3.one * 2.5f, _gameObject.transform.localScale);
        }

        private object GetPrivateField(string fieldName) =>
            typeof(HoverScaleOnPointer)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(_hover);
    }
}
