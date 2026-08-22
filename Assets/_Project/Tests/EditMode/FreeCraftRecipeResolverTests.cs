using System;
using System.Collections.Generic;
using System.Reflection;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// フリー調合で素材の組み合わせからレシピを引く処理の検証。
    /// </summary>
    public class FreeCraftRecipeResolverTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private CraftRecipeDB _database;
        private CraftRecipeData _recipe;
        private ItemData _materialA;
        private ItemData _materialB;
        private ItemData _unknownMaterial;

        [SetUp]
        public void SetUp()
        {
            _materialA = Create<ItemData>();
            _materialB = Create<ItemData>();
            _unknownMaterial = Create<ItemData>();
            _recipe = Create<CraftRecipeData>();
            _recipe.material1 = _materialA;
            _recipe.material2 = _materialB;
            _recipe.resultItem = Create<ItemData>();
            _database = Create<CraftRecipeDB>();
            SetRecipes(_database, new List<CraftRecipeData> { _recipe });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
                UnityEngine.Object.DestroyImmediate(createdObject);

            _createdObjects.Clear();
        }

        [Test]
        public void Resolve_WithMatchingMaterials_ReturnsRecipe()
        {
            var resolution = CreateResolver()
                .Resolve(Stacks(new ItemStack(_materialA), new ItemStack(_materialB)));

            Assert.IsTrue(resolution.Succeeded);
            Assert.AreSame(_recipe, resolution.Recipe);
            Assert.AreEqual(FreeCraftRecipeFailure.None, resolution.Failure);
        }

        [Test]
        public void Resolve_WithMissingMaterial_ReturnsMissingMaterials()
        {
            var resolution = CreateResolver().Resolve(Stacks(new ItemStack(_materialA)));

            Assert.IsFalse(resolution.Succeeded);
            Assert.AreEqual(FreeCraftRecipeFailure.MissingMaterials, resolution.Failure);
        }

        [Test]
        public void Resolve_WithUnknownCombination_ReturnsRecipeNotFound()
        {
            var resolution = CreateResolver()
                .Resolve(Stacks(new ItemStack(_materialA), new ItemStack(_unknownMaterial)));

            Assert.IsFalse(resolution.Succeeded);
            Assert.AreEqual(FreeCraftRecipeFailure.RecipeNotFound, resolution.Failure);
        }

        [Test]
        public void Resolve_PreservesDatabaseOrderIndependentMatching()
        {
            var resolution = CreateResolver()
                .Resolve(Stacks(new ItemStack(_materialB), new ItemStack(_materialA)));

            Assert.AreSame(_recipe, resolution.Recipe);
        }

        [Test]
        public void Resolve_UsesItemDataWithoutDependingOnStackIdentity()
        {
            var firstMaterialStack = new ItemStack(_materialA);
            var otherStackWithSameData = new ItemStack(_materialA);
            var secondMaterialStack = new ItemStack(_materialB);

            var firstResolution = CreateResolver()
                .Resolve(Stacks(firstMaterialStack, secondMaterialStack));
            var secondResolution = CreateResolver()
                .Resolve(Stacks(otherStackWithSameData, secondMaterialStack));

            Assert.AreSame(_recipe, firstResolution.Recipe);
            Assert.AreSame(_recipe, secondResolution.Recipe);
        }

        [Test]
        public void Resolve_DoesNotMutateInputStacks()
        {
            var first = new ItemStack(_materialA, 3);
            var second = new ItemStack(_materialB, 4);
            var stacks = Stacks(first, second);

            CreateResolver().Resolve(stacks);

            Assert.AreSame(first, stacks[0]);
            Assert.AreSame(second, stacks[1]);
            Assert.AreEqual(3, first.Count);
            Assert.AreEqual(4, second.Count);
        }

        [Test]
        public void Resolve_WithNullInput_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CreateResolver().Resolve(null));
        }

        [Test]
        public void Constructor_WithNullDatabase_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FreeCraftRecipeResolver(null));
        }

        [Test]
        public void FreeCraftRequest_PreservesRecipeAndStackSnapshot()
        {
            var first = new ItemStack(_materialA);
            var second = new ItemStack(_materialB);

            var request = new FreeCraftRequest(_recipe, first, second);

            Assert.AreSame(_recipe, request.Recipe);
            Assert.AreSame(first, request.FirstMaterial);
            Assert.AreSame(second, request.SecondMaterial);
        }

        [Test]
        public void FreeCraftRequest_RejectsMissingInputs()
        {
            var first = new ItemStack(_materialA);
            var second = new ItemStack(_materialB);

            Assert.Throws<ArgumentNullException>(() => new FreeCraftRequest(null, first, second));
            Assert.Throws<ArgumentNullException>(() => new FreeCraftRequest(_recipe, null, second));
            Assert.Throws<ArgumentNullException>(() => new FreeCraftRequest(_recipe, first, null));
        }

        private FreeCraftRecipeResolver CreateResolver()
        {
            return new FreeCraftRecipeResolver(_database);
        }

        private static IReadOnlyList<ItemStack> Stacks(params ItemStack[] stacks)
        {
            return stacks;
        }

        private T Create<T>()
            where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetRecipes(CraftRecipeDB database, List<CraftRecipeData> recipes)
        {
            typeof(CraftRecipeDB)
                .GetField("_recipes", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(database, recipes);
        }
    }
}
