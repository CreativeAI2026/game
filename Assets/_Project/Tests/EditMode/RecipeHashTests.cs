using CreativeAI.Crafting;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    public class RecipeHashTests
    {
        [Test]
        public void Of_IsOrderIndependent()
        {
            // A+B == B+A(RecipeHash の核となる性質)
            Assert.AreEqual(RecipeHash.Of(1, 2), RecipeHash.Of(2, 1));
            Assert.AreEqual(RecipeHash.Of(1, 2).GetHashCode(), RecipeHash.Of(2, 1).GetHashCode());
        }

        [Test]
        public void Of_DifferentPairs_AreNotEqual()
        {
            Assert.AreNotEqual(RecipeHash.Of(1, 2), RecipeHash.Of(1, 3));
            Assert.AreNotEqual(RecipeHash.Of(1, 2), RecipeHash.Of(3, 4));
        }

        [Test]
        public void Of_SameItemWithItself_IsValidKey()
        {
            // 同一アイテム2個の調合もキーになり得る
            var self = RecipeHash.Of(5, 5);
            Assert.AreEqual(RecipeHash.Of(5, 5), self);
        }

        [Test]
        public void WorksAsDictionaryKey()
        {
            var dict = new System.Collections.Generic.Dictionary<RecipeHash, int>
            {
                { RecipeHash.Of(10, 20), 99 },
            };
            Assert.IsTrue(dict.TryGetValue(RecipeHash.Of(20, 10), out var v));
            Assert.AreEqual(99, v);
        }
    }
}
