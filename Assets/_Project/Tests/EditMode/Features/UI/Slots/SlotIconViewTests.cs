using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// スロットアイコンの配置計算(透明余白を除いた実描画範囲で中央寄せ)の検証。
    /// </summary>
    public class SlotIconViewTests
    {
        [Test]
        public void CalculateVisibleContentLayout_CentersTightMeshInsteadOfTransparentCanvas()
        {
            SlotIconView.CalculateVisibleContentLayout(
                new Rect(0f, 0f, 100f, 100f),
                new Vector2(50f, 50f),
                100f,
                new[]
                {
                    new Vector2(-0.4f, -0.25f),
                    new Vector2(-0.4f, 0.25f),
                    new Vector2(0.1f, 0.25f),
                    new Vector2(0.1f, -0.25f),
                },
                new Vector2(90f, 90f),
                out Vector2 imageSize,
                out Vector2 visibleCenterOffset
            );

            Assert.That(imageSize.x, Is.EqualTo(180f).Within(0.01f));
            Assert.That(imageSize.y, Is.EqualTo(180f).Within(0.01f));
            Assert.That(visibleCenterOffset.x, Is.EqualTo(27f).Within(0.01f));
            Assert.That(visibleCenterOffset.y, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
