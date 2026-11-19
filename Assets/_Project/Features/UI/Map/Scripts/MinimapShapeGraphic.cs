using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの1つの階を、テクスチャを使わず四角形の組み合わせで描く(拡大しても線がにじまない)。
    /// 頂点はワールドの XZ そのまま(1 = 1u)。表示の大きさは RectTransform の scale で決める。
    /// 細い線は、専用のシェーダー(MinimapShape)で縁をなめらかにし、画面上の太さに下限を付ける。
    /// そのために、各四角形の頂点へ「縁までの距離の測り方」を持たせる(uv0 = 中心からの画面上のずれ、uv1 = 縁までの半幅)。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapShapeGraphic : MaskableGraphic
    {
        [SerializeField]
        private Color _floorColor = new(0.27f, 0.32f, 0.39f, 1f);

        [SerializeField, Tooltip("床の外周(吹き抜け・建物の外との境目)の線")]
        private Color _edgeColor = new(0.5f, 0.57f, 0.66f, 1f);

        [SerializeField, Tooltip("壁のかたまりの地")]
        private Color _wallColor = new(0.13f, 0.16f, 0.2f, 1f);

        [SerializeField, Tooltip("壁が床に面する縁の線")]
        private Color _wallEdgeColor = new(0.84f, 0.88f, 0.93f, 1f);

        [SerializeField]
        private Color _glassColor = new(0.55f, 0.8f, 1f, 1f);

        [SerializeField]
        private Color _fenceColor = new(0.6f, 0.65f, 0.72f, 1f);

        [SerializeField]
        private Color _doorColor = new(0.42f, 0.86f, 0.62f, 1f);

        [SerializeField]
        private Color _stairsColor = new(0.32f, 0.37f, 0.45f, 1f);

        [SerializeField]
        private Color _stairsStepColor = new(0.7f, 0.76f, 0.84f, 1f);

        [SerializeField, Tooltip("床の縁取りの太さ(u)")]
        private float _edgeWidth = 0.5f;

        [SerializeField, Tooltip("階段の段の線の間隔(u)")]
        private float _stepSpacing = 0.9f;

        [SerializeField, Tooltip("階段の段の線の太さ(u)")]
        private float _stepWidth = 0.25f;

        // 縁の種類。Line は細い方向だけ、Outline は両方向を、なめらかにして太さの下限を保つ。Hard はなめらかにしない(塗りつぶし)。
        private enum EdgeStyle
        {
            Hard,
            Line,
            Outline,
        }

        private const float AntiAliasMarginPixels = 1f; // なめらかな縁を描くため、四角形を外へ広げる量(画面上の px)
        private const float NoEdge = 10000f; // 縁までの半幅がこれ以上なら、その方向はなめらかにしない

        [SerializeField, Tooltip("線の太さの下限(画面上の px)。これより細い線は、この太さで描く")]
        private float _minLinePixels = 1f;

        private static readonly IReadOnlyList<MinimapLayout.Level> NoLevels =
            new MinimapLayout.Level[0];

        private IReadOnlyList<MinimapLayout.Level> _levels = NoLevels;
        private float _builtScale = 1f; // 頂点を作ったときの、1u あたりの画面上の px

        /// <summary>
        /// 描く階(同じ高さの別の建物を並べて描くときは複数)。形を作り直すので、変わったときだけ呼ぶ。
        /// 渡したリストは描き直すたびに読むので、中身を変えたらもう一度呼ぶ。
        /// </summary>
        public void Show(IReadOnlyList<MinimapLayout.Level> levels)
        {
            _levels = levels ?? NoLevels;
            FitRectToLevels();
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // シェーダーへ縁の情報を渡す uv1 を、キャンバスが送るようにする
            var parent = canvas;
            if (parent == null)
                return;
            parent.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            parent.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        // 拡大率(マップ画面の大きさ変更など)が変わったら、画面上の px で決める縁の幅を作り直す。
        private void Update()
        {
            if (Mathf.Abs(ScreenScale() - _builtScale) > _builtScale * 0.005f)
                SetVerticesDirty();
        }

        // 1u が画面上で何 px か(Overlay のキャンバスでは、全体の拡大率を含む lossyScale がそのまま px)。
        private float ScreenScale() => Mathf.Max(Mathf.Abs(rectTransform.lossyScale.x), 0.0001f);

        // 矩形を描く範囲に合わせる(頂点はワールド座標のまま)。大きさ 0 だと RectMask2D に範囲外として間引かれる
        private void FitRectToLevels()
        {
            if (_levels.Count == 0)
                return;
            var bounds = _levels[0].Bounds;
            for (var i = 1; i < _levels.Count; i++)
            {
                var b = _levels[i].Bounds;
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, b.xMin),
                    Mathf.Min(bounds.yMin, b.yMin),
                    Mathf.Max(bounds.xMax, b.xMax),
                    Mathf.Max(bounds.yMax, b.yMax)
                );
            }
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;
            rectTransform.sizeDelta = bounds.size;
            rectTransform.pivot = new Vector2(
                -bounds.xMin / bounds.width,
                -bounds.yMin / bounds.height
            );
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _builtScale = ScreenScale();

            // 縁取り: 床を少し広げて先に塗り、上から床を重ねると外周にだけ線が残る
            foreach (var level in _levels)
            foreach (var shape in level.Shapes)
                if (shape.Kind is MinimapLayout.ShapeKind.Floor or MinimapLayout.ShapeKind.Stairs)
                    AddRect(
                        vh,
                        Expand(shape.Rect, _edgeWidth),
                        _edgeColor,
                        EdgeStyle.Outline,
                        _edgeWidth
                    );

            foreach (var level in _levels)
            foreach (var shape in level.Shapes)
            {
                switch (shape.Kind)
                {
                    case MinimapLayout.ShapeKind.Floor:
                        AddRect(vh, shape.Rect, _floorColor);
                        break;
                    case MinimapLayout.ShapeKind.Stairs:
                        AddStairs(vh, shape);
                        break;
                    case MinimapLayout.ShapeKind.Wall:
                        AddRect(vh, shape.Rect, _wallColor);
                        break;
                    case MinimapLayout.ShapeKind.WallEdge:
                        AddRect(vh, shape.Rect, _wallEdgeColor, EdgeStyle.Line);
                        break;
                    case MinimapLayout.ShapeKind.Fence:
                        AddRect(vh, shape.Rect, _fenceColor, EdgeStyle.Line);
                        break;
                    case MinimapLayout.ShapeKind.Glass:
                        AddRect(vh, shape.Rect, _glassColor, EdgeStyle.Line);
                        break;
                    case MinimapLayout.ShapeKind.Door:
                        AddRect(vh, shape.Rect, _doorColor, EdgeStyle.Line);
                        break;
                }
            }
        }

        /// <summary>階段。地の上に、上る向きと直交する段の線を等間隔に引く。</summary>
        private void AddStairs(VertexHelper vh, MinimapLayout.Shape shape)
        {
            var rect = shape.Rect;
            AddRect(vh, rect, _stairsColor);

            var alongX = Mathf.Abs(shape.Up.x) > Mathf.Abs(shape.Up.y);
            var length = alongX ? rect.width : rect.height;
            var count = Mathf.Max(1, Mathf.FloorToInt(length / _stepSpacing));
            var spacing = length / (count + 1);
            for (var i = 1; i <= count; i++)
            {
                var t = spacing * i - _stepWidth * 0.5f;
                var step = alongX
                    ? new Rect(rect.xMin + t, rect.yMin, _stepWidth, rect.height)
                    : new Rect(rect.xMin, rect.yMin + t, rect.width, _stepWidth);
                // 上り切る側ほど明るくして、どちらへ上るかが分かるようにする
                var progress = (float)i / (count + 1);
                var toward = alongX ? shape.Up.x : shape.Up.y;
                var brightness = toward > 0f ? progress : 1f - progress;
                AddRect(
                    vh,
                    step,
                    Color.Lerp(_stairsColor, _stairsStepColor, 0.35f + 0.65f * brightness),
                    EdgeStyle.Line
                );
            }
        }

        private static Rect Expand(Rect rect, float amount) =>
            new(
                rect.xMin - amount,
                rect.yMin - amount,
                rect.width + amount * 2f,
                rect.height + amount * 2f
            );

        /// <summary>
        /// 細い線の、太さの下限とやわらかい縁を含めた半幅。戻り値は頂点を置く半幅(u)で、
        /// <paramref name="edgeHalfPixels"/> は線そのものの半幅(画面上の px)。
        /// </summary>
        /// <param name="halfUnits">四角形の半幅(u)。</param>
        /// <param name="thicknessUnits">下限と比べる、線の太さ(u)。</param>
        /// <param name="scale">1u が画面上で何 px か。</param>
        /// <param name="minPixels">線の太さの下限(px)。</param>
        public static float LineExtent(
            float halfUnits,
            float thicknessUnits,
            float scale,
            float minPixels,
            out float edgeHalfPixels
        )
        {
            // 下限に足りない分を、両側へ半分ずつ太らせる
            var growUnits = Mathf.Max(0f, minPixels - thicknessUnits * scale) * 0.5f / scale;
            var edgeHalf = halfUnits + growUnits;
            edgeHalfPixels = edgeHalf * scale;
            return edgeHalf + AntiAliasMarginPixels / scale;
        }

        // style が Hard 以外のときは、なめらかな縁の分だけ外へ広げ、シェーダーが縁までの距離を測れるようにする。
        private void AddRect(
            VertexHelper vh,
            Rect rect,
            Color tint,
            EdgeStyle style = EdgeStyle.Hard,
            float thickness = 0f
        )
        {
            Color32 c = tint * color;
            var scale = _builtScale;
            var half = rect.size * 0.5f;
            var extent = half;
            var edgeHalfPixels = new Vector2(NoEdge, NoEdge);
            if (style != EdgeStyle.Hard)
            {
                if (style == EdgeStyle.Line)
                    thickness = Mathf.Min(rect.width, rect.height);
                if (style == EdgeStyle.Outline || half.x < half.y)
                    extent.x = LineExtent(
                        half.x,
                        thickness,
                        scale,
                        _minLinePixels,
                        out edgeHalfPixels.x
                    );
                if (style == EdgeStyle.Outline || half.x >= half.y)
                    extent.y = LineExtent(
                        half.y,
                        thickness,
                        scale,
                        _minLinePixels,
                        out edgeHalfPixels.y
                    );
            }

            var edge = new Vector4(edgeHalfPixels.x, edgeHalfPixels.y, 0f, 0f);
            var center = rect.center;
            var start = vh.currentVertCount;
            AddCorner(vh, c, center, extent, scale, -1f, -1f, edge);
            AddCorner(vh, c, center, extent, scale, -1f, 1f, edge);
            AddCorner(vh, c, center, extent, scale, 1f, 1f, edge);
            AddCorner(vh, c, center, extent, scale, 1f, -1f, edge);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        private static void AddCorner(
            VertexHelper vh,
            Color32 c,
            Vector2 center,
            Vector2 extent,
            float scale,
            float signX,
            float signY,
            Vector4 edge
        )
        {
            var offset = new Vector2(signX * extent.x, signY * extent.y);
            vh.AddVert(
                center + offset,
                c,
                new Vector4(offset.x * scale, offset.y * scale, 0f, 0f), // 中心からの画面上のずれ(px)
                edge, // 縁までの半幅(px)
                Vector3.zero,
                Vector4.zero
            );
        }
    }
}
