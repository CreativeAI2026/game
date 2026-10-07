using System.Reflection;
using CreativeAI.UI;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// レシピ一覧(6列グリッド)のキーボード移動の検証。
    /// </summary>
    public class RecipeListKeyboardNavigationTests
    {
        private const int Columns = 6;

        [Test]
        public void RightMove_SelectsNextRecipe()
        {
            Assert.AreEqual(1, MoveInGrid(0, 8, dx: 1, dy: 0));
        }

        [Test]
        public void LeftMove_FromFirst_WrapsToLast()
        {
            Assert.AreEqual(7, MoveInGrid(0, 8, dx: -1, dy: 0));
        }

        [Test]
        public void DownMove_SelectsRecipeInNextGridRow()
        {
            Assert.AreEqual(6, MoveInGrid(0, 8, dx: 0, dy: 1));
        }

        [Test]
        public void DownMove_FromLastRow_WrapsToTopOfSameColumn()
        {
            Assert.AreEqual(1, MoveInGrid(7, 8, dx: 0, dy: 1));
        }

        [Test]
        public void UpMove_FromTopRow_WrapsToBottomOfSameColumn()
        {
            Assert.AreEqual(7, MoveInGrid(1, 8, dx: 0, dy: -1));
            Assert.AreEqual(2, MoveInGrid(2, 8, dx: 0, dy: -1), "下に行が無い列はそのまま");
        }

        private static int MoveInGrid(int index, int count, int dx, int dy)
        {
            MethodInfo method = typeof(RecipeCraftPanelController).GetMethod(
                "MoveInGrid",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            Assert.IsNotNull(method);
            return (int)method.Invoke(null, new object[] { index, count, Columns, dx, dy });
        }
    }
}
