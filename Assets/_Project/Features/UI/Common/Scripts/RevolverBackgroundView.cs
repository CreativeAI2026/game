using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// リボルバー背景を指定した半径の直径サイズにし、指定した中心へ配置します。
    /// </summary>
    [ExecuteAlways]
    public sealed class RevolverBackgroundView : MonoBehaviour
    {
        [SerializeField]
        private Image _backgroundImage;

        [SerializeField]
        private RevolverTabGroup _revolverTabGroup;

        public void RefreshLayout()
        {
            if (
                _backgroundImage == null
                || _revolverTabGroup == null
                || !_revolverTabGroup.TryGetBackgroundLayout(
                    out RectTransform coordinateRoot,
                    out Sprite sprite,
                    out Vector2 circleCenter,
                    out float circleWidth
                )
            )
                return;

            _backgroundImage.sprite = sprite;
            RectTransform backgroundRect = _backgroundImage.rectTransform;
            backgroundRect.sizeDelta = new Vector2(circleWidth, circleWidth);
            backgroundRect.pivot = new Vector2(1f, 0.5f);

            Vector2 rightEdge = circleCenter + Vector2.right * (circleWidth * 0.5f);
            Vector3 rightEdgeWorld = coordinateRoot.TransformPoint(
                coordinateRoot.rect.center + rightEdge
            );
            Vector3 backgroundPosition = backgroundRect.position;
            backgroundRect.position = new Vector3(
                rightEdgeWorld.x,
                rightEdgeWorld.y,
                backgroundPosition.z
            );
        }

        private void OnEnable() => RefreshLayout();

#if UNITY_EDITOR
        private void OnValidate() => RefreshLayout();
#endif
    }
}
