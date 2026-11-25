using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 全画面マップの拡大・縮小と移動。階の全体が表示範囲に収まる大きさを 1 として、拡大率とずれを持つ。
    /// </summary>
    internal sealed class MapPanZoom
    {
        private readonly float _fitRatio;
        private readonly float _maxZoom;
        private Vector2 _baseCenter; // 階全体を収めるときの中心
        private float _basePixelsPerUnit; // 階全体を収めるときの拡大率
        private Vector2 _halfBounds; // 階の範囲の半分(見ている中心をこの中に収める)
        private float _zoom = 1f; // 階全体を収めた大きさを 1 とした倍率
        private Vector2 _pan; // 階全体の中心から、見ている中心へのずれ(ワールドの XZ)

        /// <param name="fitRatio">階全体を収めるとき、表示範囲のどれだけを使うか</param>
        /// <param name="maxZoom">拡大できる上限</param>
        public MapPanZoom(float fitRatio, float maxZoom)
        {
            _fitRatio = fitRatio;
            _maxZoom = maxZoom;
        }

        /// <summary>表示範囲の中心に来るワールドの XZ。</summary>
        public Vector2 Center { get; private set; }

        /// <summary>ワールドの 1 単位を何 px で描くか。</summary>
        public float PixelsPerUnit { get; private set; }

        /// <summary>全体表示に戻す。</summary>
        public void Reset()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
        }

        /// <summary>階の範囲と表示範囲の大きさから、中心と拡大率を決め直す。</summary>
        public void Fit(Rect bounds, Vector2 viewportSize)
        {
            _halfBounds = bounds.size * 0.5f;
            _baseCenter = bounds.center;
            _basePixelsPerUnit =
                Mathf.Min(viewportSize.x / bounds.width, viewportSize.y / bounds.height)
                * _fitRatio;
            Center = _baseCenter + _pan;
            PixelsPerUnit = _basePixelsPerUnit * _zoom;
        }

        /// <summary>表示範囲の中心から localPoint(px)にある点を動かさないまま、factor 倍に拡大・縮小する。</summary>
        public void ZoomAt(Vector2 localPoint, float factor)
        {
            var zoom = Mathf.Clamp(_zoom * factor, 1f, _maxZoom);
            if (Mathf.Approximately(zoom, _zoom))
                return;
            var world = Center + localPoint / PixelsPerUnit; // マウスの下にある地図の点
            _zoom = zoom;
            PixelsPerUnit = _basePixelsPerUnit * _zoom;
            _pan = world - localPoint / PixelsPerUnit - _baseCenter; // その点が同じ場所に残るように
            ClampPan();
            Center = _baseCenter + _pan;
        }

        /// <summary>画面上で pixelDelta だけドラッグした分、地図を動かす。</summary>
        public void Drag(Vector2 pixelDelta)
        {
            _pan -= pixelDelta / PixelsPerUnit;
            ClampPan();
        }

        /// <summary>ワールドの位置を、表示範囲の中心からの位置(px)に直す。</summary>
        public Vector2 ToViewport(Vector3 world) =>
            (new Vector2(world.x, world.z) - Center) * PixelsPerUnit;

        // 見ている中心が、階の範囲から出ないようにする。全体表示のときは動かさない。
        private void ClampPan()
        {
            if (_zoom <= 1.001f)
            {
                _pan = Vector2.zero;
                return;
            }
            _pan = new Vector2(
                Mathf.Clamp(_pan.x, -_halfBounds.x, _halfBounds.x),
                Mathf.Clamp(_pan.y, -_halfBounds.y, _halfBounds.y)
            );
        }
    }
}
