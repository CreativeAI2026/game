using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class ItemParameterListViewTests
    {
        private GameObject _prefab;
        private ItemParameterListView _view;

        [SetUp]
        public void SetUp()
        {
            _prefab = PrefabUtility.LoadPrefabContents(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab"
            );
            _view = _prefab.GetComponentInChildren<ItemParameterListView>(true);
            Assert.IsNotNull(_view);
        }

        [TearDown]
        public void TearDown() => PrefabUtility.UnloadPrefabContents(_prefab);

        [Test]
        public void RolledEquipment_PutsEachBonusInItsOwnValue()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.category = ItemCategory.Equipment;
            try
            {
                _view.SetItem(
                    new ItemStack(
                        item,
                        new[]
                        {
                            new RolledStat("DefensePct", 18.5f),
                            new RolledStat("CritDamage", 24f),
                        }
                    )
                );
                Assert.AreEqual(
                    "防御",
                    _view.transform.Find("Defense/Label").GetComponent<TMP_Text>().text
                );
                Assert.AreEqual("+18.5%", Value("Defense"));
                Assert.AreEqual("+24%", Value("CriticalDamage"));
                Assert.AreEqual("0%", Value("Attack"));
                Assert.AreEqual("0%", Value("CriticalRate"));
                Assert.AreEqual("0%", Value("MaxHP"));
                Assert.IsFalse(_view.transform.Find("Recovery").gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void FoodShowsRecovery_AndImportantItemClearsAllParameters()
        {
            var food = ScriptableObject.CreateInstance<FoodData>();
            var important = ScriptableObject.CreateInstance<ItemData>();
            important.category = ItemCategory.Important;
            try
            {
                _view.SetItem(new ItemStack(food, 3));
                Assert.IsTrue(_view.transform.Find("Recovery").gameObject.activeSelf);
                Assert.AreEqual("+20%", Value("Recovery"));
                Assert.IsFalse(_view.transform.Find("Defense").gameObject.activeSelf);
                _view.SetItem(new ItemStack(important));
                Assert.IsFalse(_view.gameObject.activeSelf);
                _view.SetItem(new ItemStack(food, 1));
                Assert.IsTrue(_view.gameObject.activeSelf);
                Assert.AreEqual("+20%", Value("Recovery"));
            }
            finally
            {
                Object.DestroyImmediate(food);
                Object.DestroyImmediate(important);
            }
        }

        private string Value(string row) =>
            _view.transform.Find(row + "/Value").GetComponent<TMP_Text>().text;
    }
}
