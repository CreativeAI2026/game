using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class TriangleLayout : MonoBehaviour
    {
        [SerializeField]
        private float _radius = 200f;

        [SerializeField]
        private float _startAngleDeg = 90f;

        [SerializeField]
        private float _animationDuration = 0.4f;

        [SerializeField]
        private Ease _ease = Ease.InOutQuint;

        // 現在の頂点インデックス（0=上, 1=左下, 2=右下）
        // _offsetIndex=0 のとき子[0]が上、子[1]が左下、子[2]が右下
        private int _offsetIndex = 0;
        private bool _isAnimating = false;

        private void Start()
        {
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            ApplyPositionsImmediate();
            RebindButtons();
        }

        private void Rotate(int direction, int fromVertex)
        {
            if (_isAnimating)
                return;

            // 押されたボタンのIconを特定して回転
            foreach (Transform child in transform)
            {
                int vertexIndex =
                    (
                        System.Array.IndexOf(
                            System.Linq.Enumerable.ToArray(transform.Cast<Transform>()),
                            child
                        ) + _offsetIndex
                    ) % 3;

                if (vertexIndex == fromVertex)
                {
                    var icon = child.Find("Icon");
                    if (icon != null)
                    {
                        var rt = icon.GetComponent<RectTransform>();
                        if (rt != null)
                            rt.DORotate(
                                    new Vector3(0, 360, 0),
                                    _animationDuration,
                                    RotateMode.FastBeyond360
                                )
                                .SetEase(_ease);
                    }
                    break;
                }
            }

            _offsetIndex = (_offsetIndex + direction + 3) % 3;
            AnimateToPositions();
            RebindButtons();
        }

        private void RebindButtons()
        {
            int i = 0;
            foreach (Transform child in transform)
            {
                if (i >= 3)
                    break;

                var btn = child.GetComponent<Button>();
                if (btn != null)
                {
                    // TriangleLayoutRotator が登録したリスナーだけ管理する
                    // RemoveAllListeners は使わない
                    btn.onClick.RemoveListener(RotateCounterClockwise);
                    btn.onClick.RemoveListener(RotateClockwise);

                    int vertexIndex = (i + _offsetIndex) % 3;

                    if (vertexIndex == 2)
                        btn.onClick.AddListener(RotateClockwise);
                    else if (vertexIndex == 1)
                        btn.onClick.AddListener(RotateCounterClockwise);
                }
                i++;
            }
        }

        // 右下クリック → 反時計回り（右下が上へ来る）
        public void RotateCounterClockwise() => Rotate(-1, 1);

        // 左下クリック → 時計回り（左下が上へ来る）
        public void RotateClockwise() => Rotate(1, 2);

        public void RotateSlotToTop(int slotIndex)
        {
            if (_isAnimating || slotIndex < 0 || slotIndex >= 3)
                return;

            int currentVertex = (slotIndex + _offsetIndex) % 3;
            if (currentVertex == 1)
                RotateCounterClockwise();
            else if (currentVertex == 2)
                RotateClockwise();
        }

        private Vector2 GetVertex(int vertexIndex)
        {
            float angleDeg = _startAngleDeg + vertexIndex * 120f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(_radius * Mathf.Cos(angleRad), _radius * Mathf.Sin(angleRad));
        }

        private void AnimateToPositions()
        {
            _isAnimating = true;
            int completed = 0;
            int childCount = 0;

            foreach (Transform child in transform)
            {
                if (childCount >= 3)
                    break;
                childCount++;
            }

            int i = 0;
            foreach (Transform child in transform)
            {
                if (i >= 3)
                    break;

                // 子iは頂点 (i + _offsetIndex) % 3 へ移動
                int vertexIndex = (i + _offsetIndex) % 3;
                Vector2 target = GetVertex(vertexIndex);

                if (child is RectTransform rt)
                {
                    DOTween
                        .To(
                            () => rt.anchoredPosition,
                            x => rt.anchoredPosition = x,
                            target,
                            _animationDuration
                        )
                        .SetEase(_ease)
                        .OnComplete(() =>
                        {
                            completed++;
                            if (completed >= childCount)
                                _isAnimating = false;
                        });
                }
                else
                {
                    DOTween
                        .To(
                            () => (Vector2)child.localPosition,
                            x => child.localPosition = x,
                            target,
                            _animationDuration
                        )
                        .SetEase(_ease)
                        .OnComplete(() =>
                        {
                            completed++;
                            if (completed >= childCount)
                                _isAnimating = false;
                        });
                }

                i++;
            }
        }

        private void ApplyPositionsImmediate()
        {
            int i = 0;
            foreach (Transform child in transform)
            {
                if (i >= 3)
                    break;

                int vertexIndex = (i + _offsetIndex) % 3;
                Vector2 pos = GetVertex(vertexIndex);

                if (child is RectTransform rt)
                    rt.anchoredPosition = pos;
                else
                    child.localPosition = pos;

                i++;
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => ApplyPositionsImmediate();
#endif
    }
}
