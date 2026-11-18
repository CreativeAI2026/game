using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの記号(プレイヤーの矢印・ピン・丸)を、テクスチャを使わず三角形で描く。
    /// RectTransform の大きさいっぱいに描き、上(+Y)が前。縁取り付き。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapIconGraphic : MaskableGraphic
    {
        /// <summary>記号の形。</summary>
        public enum IconShape
        {
            Arrow,
            Pin,
            Dot,
        }

        private const int PinArcSteps = 16; // ピンの頭の丸さの細かさ
        private const float PinHoleRatio = 0.3f; // ピンの穴の大きさ(頭の半径に対する割合)

        [SerializeField]
        private IconShape _shape = IconShape.Arrow;

        [SerializeField]
        private Color _outlineColor = new(0.05f, 0.07f, 0.1f, 0.9f);

        [SerializeField, Tooltip("縁取りの太さ(大きさに対する割合)")]
        private float _outlineRatio = 0.08f;

        /// <summary>記号の形を替える(実行時に作る記号用)。</summary>
        public void SetShape(IconShape shape)
        {
            _shape = shape;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var outline = Mathf.Min(rect.width, rect.height) * _outlineRatio;
            var inner = new Rect(
                rect.xMin + outline,
                rect.yMin + outline,
                rect.width - outline * 2f,
                rect.height - outline * 2f
            );
            AddShape(vh, rect, _outlineColor);
            AddShape(vh, inner, color);
            if (_shape == IconShape.Pin)
                AddPinHole(vh, inner);
        }

        // ピンの頭の中心に、縁取りと同じ色の穴を描く。
        private void AddPinHole(VertexHelper vh, Rect r)
        {
            var radius = Mathf.Min(r.width * 0.5f, r.height * 0.5f);
            AddCircle(
                vh,
                new Vector2(r.center.x, r.yMax - radius),
                radius * PinHoleRatio,
                _outlineColor
            );
        }

        private static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color32 c)
        {
            var start = vh.currentVertCount;
            vh.AddVert(center, c, Vector4.zero);
            for (var i = 0; i <= PinArcSteps; i++)
            {
                var angle = Mathf.PI * 2f * i / PinArcSteps;
                vh.AddVert(
                    center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    c,
                    Vector4.zero
                );
            }
            for (var i = 0; i < PinArcSteps; i++)
                vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }

        private void AddShape(VertexHelper vh, Rect r, Color32 c)
        {
            var start = vh.currentVertCount;
            if (_shape == IconShape.Arrow)
            {
                // 先のとがった矢じり(後ろを少しえぐる)
                vh.AddVert(new Vector3(r.center.x, r.yMax), c, Vector4.zero);
                vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector4.zero);
                vh.AddVert(new Vector3(r.center.x, r.yMin + r.height * 0.28f), c, Vector4.zero);
                vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector4.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
                return;
            }

            if (_shape == IconShape.Dot)
            {
                AddCircle(vh, r.center, Mathf.Min(r.width, r.height) * 0.5f, c);
                return;
            }

            // 丸い頭と下向きの先端のしずく形(フィールドのピンと同じ形)。頭の中心から扇状に塗る。
            var radius = Mathf.Min(r.width * 0.5f, r.height * 0.5f);
            var head = new Vector2(r.center.x, r.yMax - radius);
            var reach = Mathf.Max(head.y - r.yMin, radius * 1.01f);
            // 先端から頭の円へ引いた接線の接点。ここから円の上側を回って反対の接点までを描く。
            var tangent = Mathf.Acos(radius / reach);
            vh.AddVert(head, c, Vector4.zero);
            for (var i = 0; i <= PinArcSteps; i++)
            {
                var angle =
                    -Mathf.PI * 0.5f + tangent + (Mathf.PI * 2f - tangent * 2f) * i / PinArcSteps;
                vh.AddVert(
                    head + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    c,
                    Vector4.zero
                );
            }
            vh.AddVert(new Vector3(r.center.x, r.yMin), c, Vector4.zero);
            for (var i = 0; i <= PinArcSteps; i++)
                vh.AddTriangle(start, start + 1 + i, start + 2 + i);
            vh.AddTriangle(start, start + 2 + PinArcSteps, start + 1); // 先端から最初の接点までの面
        }
    }
}
