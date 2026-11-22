using System.Collections.Generic;
using CreativeAI.EditorTools;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ミニマップの距離フィールドの計算の検証(床・壁の縁までの距離、線の芯までの距離、8 ビットへの詰め方)。
    /// 1 マスだけの図で、縁が 1 テクセル未満の精度で残り、四角形の角が丸まらないことを確かめる。
    /// </summary>
    public sealed class MinimapDistanceFieldTests
    {
        private const float Cell = 4f; // Spread の 2 倍以上

        // 中央の 1 マスだけが真の 3x3(マスの中心は (0, 0)、範囲は -2..2)
        private static bool[,] CenterCell()
        {
            var mask = new bool[3, 3];
            mask[1, 1] = true;
            return mask;
        }

        // 3x3 の表の中心マスを、ワールドの原点に置くための原点(マス (1, 1) の中心 = (0, 0))
        private static readonly Vector2 Origin = new(-Cell, -Cell);

        [Test]
        public void SignedDistance_内側は縁までの距離で正になる()
        {
            var d = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(1f, 0f)
            );

            Assert.AreEqual(1f, d, 1e-4f);
        }

        [Test]
        public void SignedDistance_外側は縁までの距離で負になる()
        {
            var d = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(3f, 0f)
            );

            Assert.AreEqual(-1f, d, 1e-4f);
        }

        [Test]
        public void SignedDistance_縁の上ではほぼ0になる()
        {
            var inside = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(1.99f, 0f)
            );
            var outside = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(2.01f, 0f)
            );

            Assert.AreEqual(0.01f, inside, 1e-4f);
            Assert.AreEqual(-0.01f, outside, 1e-4f);
        }

        [Test]
        public void SignedDistance_角の外側は縦横の大きい方で測り丸まらない()
        {
            // 角 (2, 2) から斜めに (1, 1) ずれた点。ユークリッド距離なら約 1.41、チェビシェフ距離なら 1
            var d = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(3f, 3f)
            );

            Assert.AreEqual(-1f, d, 1e-4f);
        }

        [Test]
        public void SignedDistance_遠い所はSpreadで丸める()
        {
            var far = MinimapDistanceField.SignedDistance(
                CenterCell(),
                Origin,
                Cell,
                new Vector2(40f, 0f)
            );
            var deep = MinimapDistanceField.SignedDistance(AllTrue(), Origin, Cell, Vector2.zero);

            Assert.AreEqual(-MinimapField.Spread, far, 1e-4f);
            Assert.AreEqual(MinimapField.Spread, deep, 1e-4f);
        }

        [Test]
        public void SignedDistance_表の外は偽として扱い隣のマスとの境目も測る()
        {
            // 表の端のマスの外側は偽(縁がある)
            var d = MinimapDistanceField.SignedDistance(
                AllTrue(),
                Origin,
                Cell,
                new Vector2(-5f, 0f)
            ); // 表の左端のマス(-6..-2)の外へ 1 のところ

            Assert.AreEqual(1f, d, 1e-4f);
        }

        [Test]
        public void LineDistance_太さの半分以下の所がちょうど線の四角形になる()
        {
            var rect = new Rect(-2f, -0.3f, 4f, 0.6f);

            Assert.AreEqual(
                0.3f,
                MinimapDistanceField.LineDistance(rect, new Vector2(1.99f, 0.3f)),
                1e-4f
            );
            Assert.AreEqual(
                0.3f,
                MinimapDistanceField.LineDistance(rect, new Vector2(2f, 0f)),
                1e-4f
            );
            Assert.Greater(MinimapDistanceField.LineDistance(rect, new Vector2(2.05f, 0f)), 0.3f);
            Assert.Greater(MinimapDistanceField.LineDistance(rect, new Vector2(0f, 0.35f)), 0.3f);
        }

        [Test]
        public void LineDistance_正方形は点として測り四角形のまま角が残る()
        {
            var rect = new Rect(-0.35f, -0.35f, 0.7f, 0.7f);

            Assert.AreEqual(
                0.35f,
                MinimapDistanceField.LineDistance(rect, new Vector2(0.35f, 0.35f)),
                1e-4f
            );
            Assert.AreEqual(0f, MinimapDistanceField.LineDistance(rect, Vector2.zero), 1e-4f);
        }

        [Test]
        public void Encode_8ビットに詰めて戻した誤差が小さい()
        {
            foreach (var d in new[] { -1.7f, -0.5f, -0.01f, 0f, 0.01f, 0.5f, 1.7f })
                Assert.AreEqual(
                    d,
                    MinimapField.DecodeSigned(MinimapField.EncodeSigned(d)),
                    0.01f,
                    $"符号付き {d}"
                );
            foreach (var d in new[] { 0f, 0.3f, 1f, 1.9f })
                Assert.AreEqual(
                    d,
                    MinimapField.DecodeUnsigned(MinimapField.EncodeUnsigned(d)),
                    0.01f,
                    $"符号なし {d}"
                );
        }

        [Test]
        public void Build_床と壁と線がテクセルの位置に焼かれる()
        {
            // 床のあるマスは中央の 1 マス。壁はなし、中央マスの西の縁に線を 1 本
            var ground = CenterCell();
            var none = new bool[3, 3];
            var area = MinimapField.FieldRect(new Rect(-2f, -2f, 4f, 4f));
            var lines = new List<MinimapDistanceField.Line>
            {
                new(MinimapField.LineKind.WallEdge, new Rect(-2f, -2f, 0.6f, 4f)),
            };

            var image = MinimapDistanceField.Build(area, Origin, Cell, ground, none, none, lines);

            Assert.AreEqual(Mathf.CeilToInt(8f * MinimapField.TexelsPerUnit), image.Width);
            Assert.AreEqual(image.Width * image.Height, image.Field.Length);

            // 範囲の中心のテクセル: 床の内側(R が 128 より大)で、壁も階段も無く、線からは遠い
            var center = image.Field[(image.Height / 2) * image.Width + image.Width / 2];
            Assert.Greater(center.r, 128);
            Assert.Less(center.g, 128);
            Assert.Less(center.a, 128);

            // 線の上のテクセル(x = -1.8 付近、y = 0): 線の中で、種類は壁の縁
            var x = Mathf.FloorToInt((-1.8f - area.xMin) / area.width * image.Width);
            var y = image.Height / 2;
            var onLine = image.Field[y * image.Width + x];
            Assert.Less(MinimapField.DecodeUnsigned(onLine.b), 0.3f);
            Assert.AreEqual((byte)MinimapField.LineKind.WallEdge, image.Ids[y * image.Width + x]);

            // 線から遠いテクセルには、線の種類が付かない
            Assert.AreEqual(0, image.Ids[y * image.Width + image.Width / 2 + 3]);
        }

        [Test]
        public void Build_太さの違う線が重なる所は深く覆う方の線を残す()
        {
            // 壁の縁(半幅 0.3、芯は y = 0)と扉(半幅 0.7、芯は y = 1.25)を横に並べる。
            // テクセル (0.2, 0.6) は壁の縁の芯の方が近い(0.6 < 0.65)が、覆っているのは扉だけ
            var none = new bool[3, 3];
            var area = MinimapField.FieldRect(new Rect(-2f, -2f, 4f, 4f));
            var lines = new List<MinimapDistanceField.Line>
            {
                new(MinimapField.LineKind.WallEdge, new Rect(-2f, -0.3f, 4f, 0.6f)),
                new(MinimapField.LineKind.Door, new Rect(-2f, 0.55f, 4f, 1.4f)),
            };

            var image = MinimapDistanceField.Build(area, Origin, Cell, none, none, none, lines);

            var x = Mathf.FloorToInt((0.2f - area.xMin) / area.width * image.Width);
            var y = Mathf.FloorToInt((0.6f - area.yMin) / area.height * image.Height);
            var i = y * image.Width + x;
            Assert.AreEqual((byte)MinimapField.LineKind.Door, image.Ids[i]);
            Assert.Less(MinimapField.DecodeUnsigned(image.Field[i].b), 0.7f);
        }

        private static bool[,] AllTrue()
        {
            var mask = new bool[3, 3];
            for (var r = 0; r < 3; r++)
            for (var c = 0; c < 3; c++)
                mask[r, c] = true;
            return mask;
        }
    }
}
