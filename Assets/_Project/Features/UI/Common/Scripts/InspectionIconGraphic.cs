using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>Resolution-independent magnifying glass for detail panel headings.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InspectionIconGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center + new Vector2(-.1f, .1f) * size;
            float radius = .27f * size;
            float thickness = .075f * size;
            const int segments = 40;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                float b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 da = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 db = new(Mathf.Cos(b), Mathf.Sin(b));
                Quad(
                    vh,
                    center + da * radius,
                    center + db * radius,
                    center + db * (radius - thickness),
                    center + da * (radius - thickness)
                );
            }
            Vector2 direction = new Vector2(1, -1).normalized;
            Vector2 normal = new Vector2(1, 1).normalized * thickness * .6f;
            Vector2 start = center + direction * (radius - thickness * .5f);
            Vector2 end = center + direction * (.58f * size);
            Quad(vh, start - normal, end - normal, end + normal, start + normal);
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero);
            vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero);
            vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
