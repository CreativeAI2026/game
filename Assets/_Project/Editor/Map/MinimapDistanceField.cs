using System;
using System.Collections.Generic;
using CreativeAI.UI;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// ミニマップの距離フィールド(<see cref="MinimapField"/>)の中身を計算する。
    /// 床・壁・階段はマス目の真偽の表から、線は四角形の線の芯から、テクセルごとの距離を測る。
    /// 縁はマスの境目に沿うので、テクセルに塗り分けず、境目までの距離をそのまま測って、縁を 1 テクセル未満の精度で残す。
    /// </summary>
    public static class MinimapDistanceField
    {
        /// <summary>1本の線。<paramref name="Rect"/> は線の太さを含む四角形(ワールドの XZ)。</summary>
        public readonly struct Line
        {
            /// <summary>線の種類。</summary>
            public readonly MinimapField.LineKind Kind;

            /// <summary>線の四角形。</summary>
            public readonly Rect Rect;

            /// <summary>線の種類と四角形から作る。</summary>
            public Line(MinimapField.LineKind kind, Rect rect)
            {
                Kind = kind;
                Rect = rect;
            }
        }

        /// <summary>計算結果。画像は左下が原点で、1 行ずつ上へ並ぶ(テクスチャと同じ)。</summary>
        public sealed class Image
        {
            /// <summary>幅(テクセル)。</summary>
            public int Width;

            /// <summary>高さ(テクセル)。</summary>
            public int Height;

            /// <summary>R = 床と階段の地、G = 壁の地、B = 線、A = 階段の地。</summary>
            public Color32[] Field;

            /// <summary>線の種類(<see cref="MinimapField.LineKind"/> の値)。</summary>
            public byte[] Ids;
        }

        /// <summary>
        /// 距離フィールドを作る。
        /// </summary>
        /// <param name="area">覆うワールドの XZ の範囲。</param>
        /// <param name="origin">マス (0, 0) の中心(ワールドの XZ)。</param>
        /// <param name="cell">1 マスの一辺(u)。<see cref="MinimapField.Spread"/> の 2 倍以上であること。</param>
        /// <param name="ground">床のあるマス(階段を含む)。[行, 列]。</param>
        /// <param name="stairs">階段のマス。</param>
        /// <param name="wall">壁のマス。</param>
        /// <param name="lines">線。</param>
        public static Image Build(
            Rect area,
            Vector2 origin,
            float cell,
            bool[,] ground,
            bool[,] stairs,
            bool[,] wall,
            IReadOnlyList<Line> lines
        )
        {
            if (cell < MinimapField.Spread * 2f)
                throw new ArgumentException(
                    "マスは Spread の 2 倍以上の大きさが要る(隣のマスだけを見るため)"
                );

            var width = Mathf.Max(1, Mathf.CeilToInt(area.width * MinimapField.TexelsPerUnit));
            var height = Mathf.Max(1, Mathf.CeilToInt(area.height * MinimapField.TexelsPerUnit));
            var texel = new Vector2(area.width / width, area.height / height);
            var image = new Image
            {
                Width = width,
                Height = height,
                Field = new Color32[width * height],
                Ids = new byte[width * height],
            };

            var farLine = MinimapField.EncodeUnsigned(MinimapField.Spread);
            var lineDistance = new float[width * height];
            Array.Fill(lineDistance, MinimapField.Spread);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var p = new Vector2(
                    area.xMin + (x + 0.5f) * texel.x,
                    area.yMin + (y + 0.5f) * texel.y
                );
                image.Field[y * width + x] = new Color32(
                    MinimapField.EncodeSigned(SignedDistance(ground, origin, cell, p)),
                    MinimapField.EncodeSigned(SignedDistance(wall, origin, cell, p)),
                    farLine,
                    MinimapField.EncodeSigned(SignedDistance(stairs, origin, cell, p))
                );
            }

            // 線は、自分の周り(Spread の内側)のテクセルだけを見て、一番近い線の距離と種類を残す
            foreach (var line in lines)
            {
                var r = line.Rect;
                var x0 = Mathf.Max(
                    0,
                    Mathf.FloorToInt((r.xMin - MinimapField.Spread - area.xMin) / texel.x)
                );
                var x1 = Mathf.Min(
                    width - 1,
                    Mathf.CeilToInt((r.xMax + MinimapField.Spread - area.xMin) / texel.x)
                );
                var y0 = Mathf.Max(
                    0,
                    Mathf.FloorToInt((r.yMin - MinimapField.Spread - area.yMin) / texel.y)
                );
                var y1 = Mathf.Min(
                    height - 1,
                    Mathf.CeilToInt((r.yMax + MinimapField.Spread - area.yMin) / texel.y)
                );
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    var p = new Vector2(
                        area.xMin + (x + 0.5f) * texel.x,
                        area.yMin + (y + 0.5f) * texel.y
                    );
                    var d = LineDistance(line.Rect, p);
                    var i = y * width + x;
                    if (d >= lineDistance[i])
                        continue;
                    lineDistance[i] = d;
                    image.Ids[i] = (byte)line.Kind;
                    var c = image.Field[i];
                    c.b = MinimapField.EncodeUnsigned(d);
                    image.Field[i] = c;
                }
            }
            return image;
        }

        /// <summary>
        /// 点 <paramref name="p"/> から、マスの表 <paramref name="mask"/> の真偽の境目までの符号付き距離(u。真の側が正、
        /// <see cref="MinimapField.Spread"/> で丸める)。境目は隣り合うマスだけ調べればよい。表の外は偽として扱う。
        /// </summary>
        public static float SignedDistance(bool[,] mask, Vector2 origin, float cell, Vector2 p)
        {
            var c = Mathf.RoundToInt((p.x - origin.x) / cell);
            var r = Mathf.RoundToInt((p.y - origin.y) / cell);
            var inside = At(mask, r, c);
            var nearest = MinimapField.Spread;
            for (var dr = -1; dr <= 1; dr++)
            for (var dc = -1; dc <= 1; dc++)
            {
                if (At(mask, r + dr, c + dc) == inside)
                    continue;
                var center = origin + new Vector2((c + dc) * cell, (r + dr) * cell);
                var dx = Mathf.Max(Mathf.Abs(p.x - center.x) - cell * 0.5f, 0f);
                var dy = Mathf.Max(Mathf.Abs(p.y - center.y) - cell * 0.5f, 0f);
                nearest = Mathf.Min(nearest, Mathf.Max(dx, dy));
            }
            return inside ? nearest : -nearest;
        }

        /// <summary>
        /// 点 <paramref name="p"/> から、線の四角形の芯(長い方向の中心線を、太さの半分だけ両端から縮めたもの)までの距離。
        /// 縦横の大きい方で測るので、距離が太さの半分以下の所がちょうど四角形になる。
        /// </summary>
        public static float LineDistance(Rect rect, Vector2 p)
        {
            var center = rect.center;
            var half = Mathf.Min(rect.width, rect.height) * 0.5f;
            // 芯の線分の、長い方向の長さ(正方形なら点)
            var reach = Mathf.Max(0f, Mathf.Max(rect.width, rect.height) * 0.5f - half);
            var dx = Mathf.Abs(p.x - center.x);
            var dy = Mathf.Abs(p.y - center.y);
            if (rect.width >= rect.height)
                dx = Mathf.Max(dx - reach, 0f);
            else
                dy = Mathf.Max(dy - reach, 0f);
            return Mathf.Max(dx, dy);
        }

        private static bool At(bool[,] mask, int r, int c) =>
            r >= 0 && c >= 0 && r < mask.GetLength(0) && c < mask.GetLength(1) && mask[r, c];
    }
}
