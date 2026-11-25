using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 全画面マップの拡大・縮小と移動の計算(全体表示・マウス位置を保った拡大・範囲外へ出ない移動)の検証。
    /// </summary>
    public class MapPanZoomTests
    {
        // 幅 100 × 奥行き 50 の階を、1000 × 1000 px の範囲に余白なしで描く(1 単位 = 10 px)
        private static readonly Rect Bounds = new(0f, 0f, 100f, 50f);
        private static readonly Vector2 Viewport = new(1000f, 1000f);

        [Test]
        public void Fit_ShowsWholeLevelCentered()
        {
            var view = new MapPanZoom(1f, 5f);

            view.Fit(Bounds, Viewport);

            Assert.AreEqual(10f, view.PixelsPerUnit, 1e-4f);
            Assert.AreEqual(new Vector2(50f, 25f), view.Center);
        }

        [Test]
        public void ZoomAt_KeepsPointUnderCursor()
        {
            var view = new MapPanZoom(1f, 5f);
            view.Fit(Bounds, Viewport);
            var cursor = new Vector2(100f, 50f);
            var world = view.Center + cursor / view.PixelsPerUnit;

            view.ZoomAt(cursor, 2f);

            Assert.AreEqual(20f, view.PixelsPerUnit, 1e-4f);
            var after = view.ToViewport(new Vector3(world.x, 0f, world.y));
            Assert.AreEqual(cursor.x, after.x, 1e-3f);
            Assert.AreEqual(cursor.y, after.y, 1e-3f);
        }

        [Test]
        public void ZoomAt_StopsAtMaxZoom()
        {
            var view = new MapPanZoom(1f, 2f);
            view.Fit(Bounds, Viewport);

            view.ZoomAt(Vector2.zero, 10f);

            Assert.AreEqual(20f, view.PixelsPerUnit, 1e-4f);
        }

        [Test]
        public void Drag_AtWholeView_DoesNotMove()
        {
            var view = new MapPanZoom(1f, 5f);
            view.Fit(Bounds, Viewport);

            view.Drag(new Vector2(300f, 0f));
            view.Fit(Bounds, Viewport);

            Assert.AreEqual(new Vector2(50f, 25f), view.Center);
        }

        [Test]
        public void Drag_WhenZoomed_StaysInsideLevel()
        {
            var view = new MapPanZoom(1f, 5f);
            view.Fit(Bounds, Viewport);
            view.ZoomAt(Vector2.zero, 2f);

            view.Drag(new Vector2(-100000f, -100000f));
            view.Fit(Bounds, Viewport);

            Assert.AreEqual(100f, view.Center.x, 1e-3f);
            Assert.AreEqual(50f, view.Center.y, 1e-3f);
        }

        [Test]
        public void Reset_ReturnsToWholeView()
        {
            var view = new MapPanZoom(1f, 5f);
            view.Fit(Bounds, Viewport);
            view.ZoomAt(new Vector2(100f, 0f), 3f);

            view.Reset();
            view.Fit(Bounds, Viewport);

            Assert.AreEqual(10f, view.PixelsPerUnit, 1e-4f);
            Assert.AreEqual(new Vector2(50f, 25f), view.Center);
        }
    }
}
