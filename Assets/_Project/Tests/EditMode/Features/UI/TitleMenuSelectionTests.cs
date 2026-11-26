using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// タイトルの項目を上下に動かすときの、押せない項目の飛ばし方と端での回り方の検証。
    /// </summary>
    public class TitleMenuSelectionTests
    {
        [Test]
        public void Move_Down_GoesToTheNextItem()
        {
            Assert.AreEqual(1, TitleMenuSelection.Move(new[] { true, true }, 0, 1));
        }

        [Test]
        public void Move_Up_FromTheTop_WrapsToTheBottom()
        {
            Assert.AreEqual(1, TitleMenuSelection.Move(new[] { true, true }, 0, -1));
        }

        [Test]
        public void Move_SkipsItemsThatCannotBePressed()
        {
            Assert.AreEqual(2, TitleMenuSelection.Move(new[] { true, false, true }, 0, 1));
        }

        [Test]
        public void Move_WhenNoOtherItemIsAvailable_StaysOnTheCurrentOne()
        {
            Assert.AreEqual(0, TitleMenuSelection.Move(new[] { true, false }, 0, 1));
        }

        [Test]
        public void Move_WhenNothingIsAvailable_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, TitleMenuSelection.Move(new[] { false, false }, 0, 1));
        }
    }
}
