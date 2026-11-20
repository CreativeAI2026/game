using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// useItem の持ち物ピッカーで、上下左右の入力が選択位置をどう動かすかの検証。
    /// </summary>
    public class ItemPickerGridNavigationTests
    {
        private static readonly Vector2Int Up = new(0, 1);
        private static readonly Vector2Int Down = new(0, -1);
        private static readonly Vector2Int Left = new(-1, 0);
        private static readonly Vector2Int Right = new(1, 0);

        // 7 個・4 列: 1 行目 0..3、2 行目 4..6。「やめる」は添字 7。
        private const int Count = 7;
        private const int Cols = 4;
        private const int Cancel = 7;

        [Test]
        public void Right_MovesWithinRowAndStopsAtRowEnd()
        {
            Assert.AreEqual(1, ItemPickerGridNavigation.Move(0, Right, Count, Cols));
            Assert.AreEqual(
                3,
                ItemPickerGridNavigation.Move(3, Right, Count, Cols),
                "行末では隣の行へ回り込まない"
            );
            Assert.AreEqual(
                6,
                ItemPickerGridNavigation.Move(6, Right, Count, Cols),
                "最後の個数を越えない"
            );
        }

        [Test]
        public void Left_MovesWithinRowAndStopsAtRowStart()
        {
            Assert.AreEqual(0, ItemPickerGridNavigation.Move(1, Left, Count, Cols));
            Assert.AreEqual(
                4,
                ItemPickerGridNavigation.Move(4, Left, Count, Cols),
                "行頭では前の行へ回り込まない"
            );
        }

        [Test]
        public void Down_MovesToNextRow_ThenToCancelFromLastRow()
        {
            Assert.AreEqual(5, ItemPickerGridNavigation.Move(1, Down, Count, Cols));
            Assert.AreEqual(Cancel, ItemPickerGridNavigation.Move(5, Down, Count, Cols));
        }

        [Test]
        public void Down_IntoShorterLastRow_LandsOnLastItem()
        {
            // 列 3 の下には個数が無い(7 番目が最後)ので、最後の個数へ寄せる。
            Assert.AreEqual(6, ItemPickerGridNavigation.Move(3, Down, Count, Cols));
        }

        [Test]
        public void Up_MovesToPreviousRowAndStopsAtTop()
        {
            Assert.AreEqual(1, ItemPickerGridNavigation.Move(5, Up, Count, Cols));
            Assert.AreEqual(2, ItemPickerGridNavigation.Move(2, Up, Count, Cols));
        }

        [Test]
        public void Cancel_UpReturnsToLastItem_OtherInputsStay()
        {
            Assert.AreEqual(Count - 1, ItemPickerGridNavigation.Move(Cancel, Up, Count, Cols));
            Assert.AreEqual(Cancel, ItemPickerGridNavigation.Move(Cancel, Down, Count, Cols));
            Assert.AreEqual(Cancel, ItemPickerGridNavigation.Move(Cancel, Left, Count, Cols));
            Assert.AreEqual(Cancel, ItemPickerGridNavigation.Move(Cancel, Right, Count, Cols));
        }

        [Test]
        public void NoItems_OnlyCancelExists()
        {
            foreach (var step in new[] { Up, Down, Left, Right })
                Assert.AreEqual(0, ItemPickerGridNavigation.Move(0, step, 0, Cols));
        }

        [Test]
        public void VerticalInputWinsOverHorizontal()
        {
            Assert.AreEqual(
                5,
                ItemPickerGridNavigation.Move(1, new Vector2Int(1, -1), Count, Cols)
            );
        }
    }
}
