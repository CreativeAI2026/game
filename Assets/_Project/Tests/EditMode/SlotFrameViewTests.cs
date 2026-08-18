using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    public class SlotFrameViewTests
    {
        private GameObject _gameObject;
        private SlotFrameView _view;
        private Sprite[] _sprites;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("SlotFrame", typeof(RectTransform), typeof(Image));
            _view = _gameObject.AddComponent<SlotFrameView>();
            _sprites = new Sprite[7];
            for (int i = 0; i < _sprites.Length; i++)
            {
                var texture = new Texture2D(1, 1) { name = $"Texture{i}" };
                _sprites[i] = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            }

            TestReflection.SetField(_view, "_frame", _gameObject.GetComponent<Image>());
            TestReflection.SetField(_view, "_normalSprite", _sprites[0]);
            TestReflection.SetField(_view, "_selectedSprite", _sprites[1]);
            TestReflection.SetField(_view, "_itemSetSprite", _sprites[2]);
            TestReflection.SetField(_view, "_itemWithCountSprite", _sprites[3]);
            TestReflection.SetField(_view, "_itemSetSelectedSprite", _sprites[4]);
            TestReflection.SetField(_view, "_itemWithCountSelectedSprite", _sprites[5]);
            TestReflection.SetField(_view, "_customSelectedSprite", _sprites[6]);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var sprite in _sprites)
            {
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }

            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ItemWithCountAndSelected_UsesCombinedSprite()
        {
            var item = ScriptableObject.CreateInstance<CreativeAI.Gameplay.ItemData>();
            try
            {
                _view.SetContent(item, 2);
                _view.SetSelected(true);

                Assert.AreSame(_sprites[5], _view.ResolveSprite());
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void ItemSetAndSelected_UsesCombinedSprite()
        {
            _view.SetRole(SlotFrameRole.ItemSet);
            _view.SetSelected(true);

            Assert.AreSame(_sprites[4], _view.ResolveSprite());
        }

        [Test]
        public void CustomRole_SwitchesBetweenAssignedPair()
        {
            _view.SetRole(SlotFrameRole.Custom);
            Assert.AreSame(_sprites[0], _view.ResolveSprite());

            _view.SetSelected(true);
            Assert.AreSame(_sprites[6], _view.ResolveSprite());
        }
    }
}
