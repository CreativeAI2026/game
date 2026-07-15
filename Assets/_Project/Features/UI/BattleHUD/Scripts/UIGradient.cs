using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    public class UIGradient : BaseMeshEffect
    {
        [Header("グラデーション設定")]
        public Color leftColor = Color.white;
        public Color rightColor = Color.gray;

        /// <summary>
        /// グラデーション色の変更後にメッシュを再構築するよう通知する。
        /// </summary>
        public void SetVerticesDirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            Rect rect = graphic.rectTransform.rect;
            float minX = rect.xMin;
            float width = rect.width;

            UIVertex vertex = new UIVertex();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = (vertex.position.x - minX) / width;

                Color gradientColor = Color.Lerp(leftColor, rightColor, t);

                vertex.color = gradientColor;

                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
