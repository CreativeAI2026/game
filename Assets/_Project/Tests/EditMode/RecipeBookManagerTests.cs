using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// レシピ帳の解禁状態(documents/Specification.md §2.3.2)。
    /// 自由調合で成功した組み合わせが解禁され、セーブ/復元で保たれる。
    /// EditMode では Awake が走らないので初期解禁(SeedInitialUnlocks)は入っていない状態から始まる。
    /// </summary>
    public class RecipeBookManagerTests
    {
        private GameObject _go;
        private RecipeBookManager _book;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(RecipeBookManager));
            _book = _go.AddComponent<RecipeBookManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        /// <summary>実カタログの初期解禁と id がぶつからないよう、テスト専用の id 帯を使う。</summary>
        private CraftRecipeData MakeRecipe(int resultId)
        {
            var result = ScriptableObject.CreateInstance<FoodData>();
            result.id = resultId;
            var recipe = ScriptableObject.CreateInstance<CraftRecipeData>();
            recipe.resultItem = result;
            _assets.Add(result);
            _assets.Add(recipe);
            return recipe;
        }

        [Test]
        public void Reveal_MakesRecipeRevealed_AndIsIdempotent()
        {
            var recipe = MakeRecipe(990101);

            Assert.IsFalse(_book.IsRevealed(recipe), "調合前は未解禁");
            Assert.IsTrue(_book.Reveal(recipe), "新規解禁できたら true");
            Assert.IsTrue(_book.IsRevealed(recipe));
            Assert.IsFalse(_book.Reveal(recipe), "2回目は解禁済みなので false");
        }

        [Test]
        public void Reveal_NullOrResultlessRecipe_IsIgnored()
        {
            var broken = ScriptableObject.CreateInstance<CraftRecipeData>(); // resultItem 未設定
            _assets.Add(broken);

            Assert.IsFalse(_book.Reveal(null));
            Assert.IsFalse(_book.Reveal(broken));
            Assert.IsFalse(_book.IsRevealed(broken));
        }

        [Test]
        public void RevealedIsKeyedByResultItemId()
        {
            // キーは結果アイテムの id。別インスタンスでも同じ結果 id なら解禁済み扱いになる。
            var a = MakeRecipe(990102);
            var b = MakeRecipe(990102);

            _book.Reveal(a);

            Assert.IsTrue(_book.IsRevealed(b));
        }

        [Test]
        public void CaptureRevealed_ReturnsRevealedIds()
        {
            var r1 = MakeRecipe(990103);
            var r2 = MakeRecipe(990104);
            _book.Reveal(r1);
            _book.Reveal(r2);

            var captured = _book.CaptureRevealed().ToList();

            CollectionAssert.Contains(captured, 990103);
            CollectionAssert.Contains(captured, 990104);
        }

        [Test]
        public void RestoreRevealed_ReplacesTheWholeSet()
        {
            var kept = MakeRecipe(990105);
            var dropped = MakeRecipe(990106);
            _book.Reveal(dropped);

            _book.RestoreRevealed(new[] { 990105 });

            Assert.IsTrue(_book.IsRevealed(kept), "セーブに入っていたものは解禁される");
            Assert.IsFalse(
                _book.IsRevealed(dropped),
                "セーブに無かったものは落ちる(丸ごと差し替え)"
            );
        }

        [Test]
        public void RestoreRevealed_Null_ClearsToInitialUnlocksOnly()
        {
            var recipe = MakeRecipe(990107);
            _book.Reveal(recipe);

            _book.RestoreRevealed(null);

            Assert.IsFalse(_book.IsRevealed(recipe));
        }

        [Test]
        public void RestoreRevealed_AlwaysReAddsCatalogInitialUnlocks()
        {
            // 初期解禁(showInRecipeCraft)はセーブ後にカタログへ追加されても常に含まれる。
            // カタログ由来の集合を基準に、復元してもそれが失われないことだけを見る。
            _book.RestoreRevealed(new List<int>());
            var initial = _book.CaptureRevealed().ToList();

            var extra = MakeRecipe(990108);
            _book.Reveal(extra);
            _book.RestoreRevealed(new List<int>());

            CollectionAssert.AreEquivalent(initial, _book.CaptureRevealed().ToList());
        }
    }
}
