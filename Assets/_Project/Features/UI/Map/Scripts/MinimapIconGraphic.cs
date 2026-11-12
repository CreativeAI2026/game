using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// ミニマップの記号(プレイヤーの矢印・ピン)を、テクスチャを使わず三角形で描く。
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
        }

        [SerializeField]
        private IconShape _shape = IconShape.Arrow;

        [SerializeField]
        private Color _outlineColor = new(0.05f, 0.07f, 0.1f, 0.9f);

        [SerializeField, Tooltip("縁取りの太さ(大きさに対する割合)")]
        private float _outlineRatio = 0.12f;

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

            // ひし形の頭と下向きの先端(しずく形のピンを簡略化した形)
            var mid = r.yMin + r.height * 0.45f;
            vh.AddVert(new Vector3(r.center.x, r.yMax), c, Vector4.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax - r.height * 0.3f), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x + r.width * 0.28f, mid), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x, r.yMin), c, Vector4.zero);
            vh.AddVert(new Vector3(r.center.x - r.width * 0.28f, mid), c, Vector4.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax - r.height * 0.3f), c, Vector4.zero);
            for (var i = 1; i < 5; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
