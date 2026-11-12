using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// マップのピンの記号を、表示中のピンの数だけ出して置き、別の階のピンを薄くすることの検証。
    /// 記号を使い回しても、担当のピンが替わったときは階を調べ直すことを確かめる。
    /// </summary>
    public sealed class MapPinIconPoolTests
    {
        private const float OtherLevelAlpha = 0.4f;

        // 1F(床面 0)と 2F(床面 10)の建物
        private static readonly Rect Bounds = new(0f, 0f, 100f, 100f);

        private sealed class FakePlacement : MapPinIconPool.IPlacement
        {
            public int ShownLevel;

            public Vector2 ToIconPosition(Vector3 world) => new(world.x, world.z);

            public bool IsShownLevel(int levelIndex) => levelIndex == ShownLevel;
        }

        private MinimapLayout _layout;
        private GameObject _root;
        private MinimapIconGraphic _template;
        private readonly List<MapPin> _pins = new();
        private readonly FakePlacement _placement = new();

        [SetUp]
        public void SetUp()
        {
            _layout = ScriptableObject.CreateInstance<MinimapLayout>();
            _layout.SetLevels(
                new List<MinimapLayout.Level>
                {
                    new()
                    {
                        BuildingId = "A",
                        Label = "1F",
                        Height = 0f,
                        Bounds = Bounds,
                    },
                    new()
                    {
                        BuildingId = "A",
                        Label = "2F",
                        Height = 10f,
                        Bounds = Bounds,
                    },
                }
            );

            _root = new GameObject("Pins", typeof(RectTransform));
            var template = new GameObject("Pin", typeof(RectTransform));
            template.transform.SetParent(_root.transform, false);
            _template = template.AddComponent<MinimapIconGraphic>();
            _placement.ShownLevel = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pin in _pins)
                RemovePin(pin);
            _pins.Clear();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_layout);
        }

        // EditMode では Awake・OnEnable が走らないので、明示的に呼んで MapPin.Active に載せる
        private MapPin AddPin(Vector3 position)
        {
            var pin = new GameObject("MapPin").AddComponent<MapPin>();
            pin.transform.position = position;
            TestReflection.Invoke(pin, "Awake");
            TestReflection.Invoke(pin, "OnEnable");
            _pins.Add(pin);
            return pin;
        }

        private static void RemovePin(MapPin pin)
        {
            TestReflection.Invoke(pin, "OnDisable");
            Object.DestroyImmediate(pin.gameObject);
        }

        private List<MinimapIconGraphic> ShownIcons()
        {
            var icons = new List<MinimapIconGraphic>();
            foreach (Transform child in _root.transform)
                if (child.gameObject.activeSelf)
                    icons.Add(child.GetComponent<MinimapIconGraphic>());
            return icons;
        }

        [Test]
        public void Constructor_HidesTemplate()
        {
            _ = new MapPinIconPool(_template);

            Assert.IsFalse(_template.gameObject.activeSelf);
        }

        [Test]
        public void Refresh_PlacesAnIconForEachPin_AndDimsOtherLevels()
        {
            AddPin(new Vector3(10f, 0f, 20f)); // 1F
            AddPin(new Vector3(30f, 10f, 40f)); // 2F
            var pool = new MapPinIconPool(_template);

            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            var icons = ShownIcons();
            Assert.AreEqual(2, icons.Count);
            Assert.AreEqual(new Vector2(10f, 20f), icons[0].rectTransform.anchoredPosition);
            Assert.AreEqual(1f, icons[0].color.a);
            Assert.AreEqual(new Vector2(30f, 40f), icons[1].rectTransform.anchoredPosition);
            Assert.AreEqual(OtherLevelAlpha, icons[1].color.a);
        }

        [Test]
        public void Refresh_HidesIconsLeftOver_WhenPinsAreGone()
        {
            AddPin(new Vector3(10f, 0f, 20f));
            var removed = AddPin(new Vector3(30f, 0f, 40f));
            var pool = new MapPinIconPool(_template);
            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            RemovePin(removed);
            _pins.Remove(removed);
            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            Assert.AreEqual(1, ShownIcons().Count);
        }

        [Test]
        public void Refresh_ChecksTheLevelAgain_WhenAnIconIsReusedForAnotherPin()
        {
            // 1つ目の記号は最初 2F のピンを出し、ピンが消えたあと 1F のピンを出す
            var upstairs = AddPin(new Vector3(10f, 10f, 20f));
            var pool = new MapPinIconPool(_template);
            pool.Refresh(_layout, _placement, OtherLevelAlpha);
            Assert.AreEqual(OtherLevelAlpha, ShownIcons()[0].color.a);

            RemovePin(upstairs);
            _pins.Remove(upstairs);
            AddPin(new Vector3(10f, 0f, 20f));
            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            Assert.AreEqual(1f, ShownIcons()[0].color.a);
        }

        [Test]
        public void Refresh_FollowsTheShownLevel()
        {
            AddPin(new Vector3(10f, 10f, 20f)); // 2F
            var pool = new MapPinIconPool(_template);
            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            _placement.ShownLevel = 1;
            pool.Refresh(_layout, _placement, OtherLevelAlpha);

            Assert.AreEqual(1f, ShownIcons()[0].color.a);
        }
    }
}
