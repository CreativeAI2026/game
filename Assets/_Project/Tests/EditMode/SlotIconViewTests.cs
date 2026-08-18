using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    public class SlotIconViewTests
    {
        private GameObject _fitObject;
        private GameObject _iconObject;
        private Texture2D _texture;
        private Sprite _sprite;

        [TearDown]
        public void TearDown()
        {
            if (_sprite != null)
                Object.DestroyImmediate(_sprite);
            if (_texture != null)
                Object.DestroyImmediate(_texture);
            if (_fitObject != null)
                Object.DestroyImmediate(_fitObject);
        }

        [Test]
        public void SetIcon_CentersVisibleTightMeshInsteadOfTransparentCanvas()
        {
            _fitObject = new GameObject("Fit", typeof(RectTransform));
            _iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            _iconObject.transform.SetParent(_fitObject.transform, false);

            var fitRect = _fitObject.GetComponent<RectTransform>();
            fitRect.sizeDelta = new Vector2(100f, 100f);

            _texture = new Texture2D(100, 100);
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, 100f, 100f),
                new Vector2(0.5f, 0.5f),
                100f
            );
            _sprite.OverrideGeometry(
                new[]
                {
                    new Vector2(-0.4f, -0.25f),
                    new Vector2(-0.4f, 0.25f),
                    new Vector2(0.1f, 0.25f),
                    new Vector2(0.1f, -0.25f),
                },
                new ushort[] { 0, 1, 2, 0, 2, 3 }
            );

            var view = _fitObject.AddComponent<SlotIconView>();
            TestReflection.SetField(view, "_image", _iconObject.GetComponent<Image>());
            TestReflection.SetField(view, "_fitRect", fitRect);
            TestReflection.SetField(view, "_fillRatio", 0.9f);

            view.SetIcon(_sprite);

            var iconRect = _iconObject.GetComponent<RectTransform>();
            Assert.That(iconRect.rect.size.x, Is.EqualTo(180f).Within(0.01f));
            Assert.That(iconRect.rect.size.y, Is.EqualTo(180f).Within(0.01f));
            Assert.That(iconRect.anchoredPosition.x, Is.EqualTo(27f).Within(0.01f));
            Assert.That(iconRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
