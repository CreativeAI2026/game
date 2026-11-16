using System.Collections.Generic;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ミニマップが位置から建物と階を選び、階の名前を出すことの検証。
    /// 同じ高さに別の建物があっても取り違えず、階段の途中では下の階のままであることを確かめる。
    /// </summary>
    public sealed class MinimapLayoutTests
    {
        private static readonly Rect MainBounds = new(0f, 0f, 100f, 100f);
        private static readonly Rect LabBounds = new(-200f, 0f, 100f, 100f);

        private MinimapLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = ScriptableObject.CreateInstance<MinimapLayout>();
            _layout.SetLevels(
                new List<MinimapLayout.Level>
                {
                    Level("Area01", "", "1F", -44.8f, MainBounds), // 0
                    Level("Area01", "", "2F", -36.8f, MainBounds), // 1
                    Level("Area01", "", "3F", -28.8f, MainBounds), // 2
                    Level("Lab", "研究棟", "1F", -60.8f, LabBounds), // 3
                    Level("Lab", "研究棟", "2F", -44.8f, LabBounds), // 4
                    Level("Lab", "研究棟", "3F", -28.8f, LabBounds), // 5
                }
            );
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_layout);

        private static MinimapLayout.Level Level(
            string id,
            string building,
            string label,
            float height,
            Rect bounds
        ) =>
            new()
            {
                BuildingId = id,
                Building = building,
                Label = label,
                Height = height,
                Bounds = bounds,
            };

        [TestCase(50f, -44.8f, 0)]
        [TestCase(50f, -40f, 0)] // 1F と 2F の間(階段の途中)は下の階
        [TestCase(50f, -37.1f, 1)] // 床面より少し下でもその階
        [TestCase(50f, -20f, 2)]
        [TestCase(-150f, -44.8f, 4)] // 同じ高さでも研究棟の中なら研究棟の階
        [TestCase(-150f, -36f, 4)] // 研究棟の 2F と 3F の間は研究棟 2F(本棟 2F と取り違えない)
        [TestCase(-150f, -100f, 3)] // どの階よりも低ければ一番下の階
        public void FindLevel_PicksBuildingByPosition_ThenFloorByHeight(
            float x,
            float y,
            int expected
        )
        {
            Assert.AreEqual(expected, _layout.FindLevel(new Vector3(x, y, 50f)));
        }

        [Test]
        public void FindLevel_OutsideAllBuildings_UsesNearestBuilding()
        {
            Assert.AreEqual(5, _layout.FindLevel(new Vector3(-250f, -28.8f, 50f)));
        }

        [Test]
        public void FindLevel_NoLevels_ReturnsMinusOne()
        {
            _layout.SetLevels(new List<MinimapLayout.Level>());
            Assert.AreEqual(-1, _layout.FindLevel(Vector3.zero));
        }

        [Test]
        public void LevelsAtHeight_CollectsAllBuildings()
        {
            var result = new List<MinimapLayout.Level>();
            _layout.LevelsAtHeight(-44.8f, result);

            CollectionAssert.AreEqual(new[] { _layout.Levels[0], _layout.Levels[4] }, result);
        }

        [Test]
        public void Title_AddsBuildingName_OnlyWhenNamed()
        {
            Assert.AreEqual("1F", _layout.Levels[0].Title);
            Assert.AreEqual("研究棟 2F", _layout.Levels[4].Title);
        }
    }
}
