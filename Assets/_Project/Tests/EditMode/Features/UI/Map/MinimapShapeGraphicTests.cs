using System.Collections.Generic;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ミニマップの細い線が、画面上で 1px 未満にならず、縁をなめらかに描けるデータになることの検証。
    /// 太さの下限を満たすよう線が太り、塗りつぶしはなめらかにしないことを確かめる。
    /// </summary>
    public sealed class MinimapShapeGraphicTests
    {
        private const float NoEdge = 10000f;

        private GameObject _root;
        private MinimapShapeGraphic _graphic;
        private MinimapLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Shape", typeof(RectTransform));
            _graphic = _root.AddComponent<MinimapShapeGraphic>();
            _layout = ScriptableObject.CreateInstance<MinimapLayout>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_layout);
        }

        // 図形を 1 つだけ持つ階を描き、頂点を返す(拡大率は 1u = 1px)
        private List<UIVertex> Build(MinimapLayout.ShapeKind kind, Rect rect)
        {
            _layout.SetLevels(
                new List<MinimapLayout.Level>
                {
                    new()
                    {
                        BuildingId = "A",
                        Label = "1F",
                        Bounds = new Rect(0f, 0f, 100f, 100f),
                        Shapes = new List<MinimapLayout.Shape>
                        {
                            new() { Kind = kind, Rect = rect },
                        },
                    },
                }
            );
            _graphic.Show(_layout.Levels);

            using var vh = new VertexHelper();
            TestReflection.Invoke(_graphic, "OnPopulateMesh", vh);
            var vertices = new List<UIVertex>();
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                var vertex = new UIVertex();
                vh.PopulateUIVertex(ref vertex, i);
                vertices.Add(vertex);
            }
            return vertices;
        }

        [Test]
        public void LineExtent_細い線は下限の太さまで太る()
        {
            // 0.5px の線(半幅 0.25px)は、下限 1px(半幅 0.5px)になる。広げる分は縁のなめらかさ用に 1px 足す。
            var extent = MinimapShapeGraphic.LineExtent(0.25f, 0.5f, 1f, 1f, out var edgeHalf);

            Assert.AreEqual(0.5f, edgeHalf, 0.0001f);
            Assert.AreEqual(1.5f, extent, 0.0001f);
        }

        [Test]
        public void LineExtent_下限より太い線はそのまま()
        {
            var extent = MinimapShapeGraphic.LineExtent(2f, 4f, 1f, 1f, out var edgeHalf);

            Assert.AreEqual(2f, edgeHalf, 0.0001f);
            Assert.AreEqual(3f, extent, 0.0001f);
        }

        [Test]
        public void LineExtent_拡大率に応じて広げる量が変わる()
        {
            // 1u が 2px なら、0.25u の線は 0.5px。下限 1px まで太らせると半幅は 0.5px = 0.25u
            var extent = MinimapShapeGraphic.LineExtent(0.125f, 0.25f, 2f, 1f, out var edgeHalf);

            Assert.AreEqual(0.5f, edgeHalf, 0.0001f);
            Assert.AreEqual(0.75f, extent, 0.0001f);
        }

        [Test]
        public void OnPopulateMesh_細い線は細い方向だけなめらかな縁を持つ()
        {
            // 縦に長い、幅 0.4 の線
            var vertices = Build(MinimapLayout.ShapeKind.Glass, new Rect(10f, 10f, 0.4f, 20f));

            Assert.AreEqual(4, vertices.Count);
            foreach (var vertex in vertices)
            {
                Assert.AreEqual(0.5f, vertex.uv1.x, 0.0001f); // 幅の方向は下限の太さ
                Assert.AreEqual(NoEdge, vertex.uv1.y); // 長さの方向はなめらかにしない
            }
            var width = vertices[2].position.x - vertices[0].position.x;
            Assert.AreEqual(3f, width, 0.0001f); // 下限の太さ 1px + 両側のなめらかな縁 1px ずつ
            var length = vertices[2].position.y - vertices[0].position.y;
            Assert.AreEqual(20f, length, 0.0001f); // 長さは変えない
        }

        [Test]
        public void OnPopulateMesh_塗りつぶしはなめらかにしない()
        {
            var vertices = Build(MinimapLayout.ShapeKind.Wall, new Rect(10f, 10f, 20f, 20f));

            Assert.AreEqual(4, vertices.Count);
            foreach (var vertex in vertices)
            {
                Assert.AreEqual(NoEdge, vertex.uv1.x);
                Assert.AreEqual(NoEdge, vertex.uv1.y);
            }
            Assert.AreEqual(20f, vertices[2].position.x - vertices[0].position.x, 0.0001f);
        }
    }
}
