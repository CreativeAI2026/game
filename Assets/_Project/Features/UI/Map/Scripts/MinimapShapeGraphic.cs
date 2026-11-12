using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの1つの階を、テクスチャを使わず四角形の組み合わせで描く(拡大しても線がにじまない)。
    /// 頂点はワールドの XZ そのまま(1 = 1u)。表示の大きさは RectTransform の scale で決める。
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

        private static readonly IReadOnlyList<MinimapLayout.Level> NoLevels =
            new MinimapLayout.Level[0];

        private IReadOnlyList<MinimapLayout.Level> _levels = NoLevels;

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

            // 縁取り: 床を少し広げて先に塗り、上から床を重ねると外周にだけ線が残る
            foreach (var level in _levels)
            foreach (var shape in level.Shapes)
                if (shape.Kind is MinimapLayout.ShapeKind.Floor or MinimapLayout.ShapeKind.Stairs)
                    AddRect(vh, Expand(shape.Rect, _edgeWidth), _edgeColor);

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
                        AddRect(vh, shape.Rect, _wallEdgeColor);
                        break;
                    case MinimapLayout.ShapeKind.Fence:
                        AddRect(vh, shape.Rect, _fenceColor);
                        break;
                    case MinimapLayout.ShapeKind.Glass:
                        AddRect(vh, shape.Rect, _glassColor);
                        break;
                    case MinimapLayout.ShapeKind.Door:
                        AddRect(vh, shape.Rect, _doorColor);
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
                    Color.Lerp(_stairsColor, _stairsStepColor, 0.35f + 0.65f * brightness)
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

        private void AddRect(VertexHelper vh, Rect rect, Color tint)
        {
            Color32 c = tint * color;
            var start = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), c, Vector4.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), c, Vector4.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), c, Vector4.zero);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), c, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
