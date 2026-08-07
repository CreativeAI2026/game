using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class RecipeCraftAvailabilityCalculatorTests
    {
        private readonly List<Object> _createdObjects = new();
        private RecipeCraftAvailabilityCalculator _calculator;
        private CraftRecipeData _recipe;
        private ItemData _materialA;
        private ItemData _materialB;

        [SetUp]
        public void SetUp()
        {
            _calculator = new RecipeCraftAvailabilityCalculator();
            _materialA = Create<ItemData>();
            _materialB = Create<ItemData>();
            _recipe = Create<CraftRecipeData>();
            _recipe.resultItem = Create<ItemData>();
            _recipe.material1 = _materialA;
            _recipe.material2 = _materialB;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
                Object.DestroyImmediate(createdObject);

            _createdObjects.Clear();
        }

        [Test]
        public void GetMaximumCraftable_UsesSmallestUnequippedMaterialCount()
        {
            var inventory = new[]
            {
                new ItemStack(_materialA, 4),
                new ItemStack(_materialB, 2),
                new ItemStack(_materialB, 8) { IsEquipped = true },
            };

            int maximum = _calculator.GetMaximumCraftable(_recipe, inventory);

            Assert.AreEqual(2, maximum);
        }

        [Test]
        public void CanCraft_RequiresPositiveQuantityWithinMaximum()
        {
            var inventory = new[] { new ItemStack(_materialA, 2), new ItemStack(_materialB, 2) };

            Assert.IsTrue(_calculator.CanCraft(_recipe, 2, inventory));
            Assert.IsFalse(_calculator.CanCraft(_recipe, 3, inventory));
            Assert.IsFalse(_calculator.CanCraft(_recipe, 0, inventory));
        }

        [Test]
        public void HasEquippedMaterial_IsTrueOnlyWhenCraftableStockIsUnavailable()
        {
            var equippedOnly = new[]
            {
                new ItemStack(_materialA, 1) { IsEquipped = true },
                new ItemStack(_materialB, 1),
            };
            var craftable = new[] { new ItemStack(_materialA, 1), new ItemStack(_materialB, 1) };

            Assert.IsTrue(_calculator.HasEquippedMaterial(_recipe, equippedOnly));
            Assert.IsFalse(_calculator.HasEquippedMaterial(_recipe, craftable));
        }

        [Test]
        public void QuickFoodStack_IsExcludedByReferenceFromCraftAvailability()
        {
            var reservedMaterial = new ItemStack(_materialA, 2);
            var inventory = new[]
            {
                reservedMaterial,
                new ItemStack(_materialA, 1),
                new ItemStack(_materialB, 3),
            };
            var quickFood = new[] { reservedMaterial };

            Assert.AreEqual(1, _calculator.GetMaximumCraftable(_recipe, inventory, quickFood));
            Assert.IsTrue(_calculator.CanCraft(_recipe, 1, inventory, quickFood));
            Assert.IsFalse(_calculator.CanCraft(_recipe, 2, inventory, quickFood));
            Assert.IsTrue(_calculator.HasQuickFoodMaterial(_recipe, 2, inventory, quickFood));
        }

        [Test]
        public void QuickFoodWarning_IsNotSelectedWhenAnotherMaterialIsMissing()
        {
            var reservedMaterial = new ItemStack(_materialA, 2);
            var inventory = new[] { reservedMaterial };

            Assert.IsFalse(
                _calculator.HasQuickFoodMaterial(_recipe, 1, inventory, new[] { reservedMaterial })
            );
        }

        [Test]
        public void DifferentStackWithSameItem_RemainsCraftable()
        {
            var reservedMaterial = new ItemStack(_materialA, 1);
            var availableMaterial = new ItemStack(_materialA, 1);
            var inventory = new[]
            {
                reservedMaterial,
                availableMaterial,
                new ItemStack(_materialB, 1),
            };

            Assert.IsTrue(_calculator.CanCraft(_recipe, 1, inventory, new[] { reservedMaterial }));
            Assert.IsFalse(
                _calculator.HasQuickFoodMaterial(_recipe, 1, inventory, new[] { reservedMaterial })
            );
        }

        [Test]
        public void BuildMaterialRows_UsesTotalOwnedCountAndSelectedQuantity()
        {
            var inventory = new[]
            {
                new ItemStack(_materialA, 2),
                new ItemStack(_materialA, 1) { IsEquipped = true },
                new ItemStack(_materialB, 4),
            };

            var rows = _calculator.BuildMaterialRows(_recipe, 3, inventory);

            Assert.AreEqual(2, rows.Count);
            Assert.AreSame(_materialA, rows[0].Item);
            Assert.AreEqual(3, rows[0].RequiredCount);
            Assert.AreEqual(3, rows[0].AvailableCount);
            Assert.AreSame(_materialB, rows[1].Item);
            Assert.AreEqual(4, rows[1].AvailableCount);
        }

        [Test]
        public void InvalidRecipe_IsNotCraftable()
        {
            _recipe.resultItem = null;

            Assert.AreEqual(0, _calculator.GetMaximumCraftable(_recipe, null));
            Assert.IsFalse(_calculator.CanCraft(_recipe, 1, null));
            Assert.IsFalse(_calculator.HasEquippedMaterial(_recipe, null));
        }

        private T Create<T>()
            where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }
    }
}
