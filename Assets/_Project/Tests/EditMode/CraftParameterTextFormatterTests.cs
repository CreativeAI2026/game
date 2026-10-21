using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class CraftParameterTextFormatterTests
    {
        [Test]
        public void Recovery_UsesFoodRuleForBaseAndCraftedItems()
        {
            var food = ScriptableObject.CreateInstance<FoodData>();
            try
            {
                Assert.AreEqual("20%", CraftParameterTextFormatter.Recovery(food));
                var so = new SerializedObject(food);
                so.FindProperty("_craftedResult").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual("50%", CraftParameterTextFormatter.Recovery(food));
            }
            finally
            {
                Object.DestroyImmediate(food);
            }
        }

        [Test]
        public void Equipment_ShowsExactRolledValueIncludingZeroStats()
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            try
            {
                var stack = new ItemStack(
                    data,
                    RolledStat.FromVector(StatVector.Of((StatType.CritRate, 7.5f)))
                );
                Assert.AreEqual(
                    "+7.5%",
                    CraftParameterTextFormatter.Actual(StatType.CritRate, stack)
                );
                Assert.AreEqual(
                    "+0%",
                    CraftParameterTextFormatter.Actual(StatType.DefensePct, stack)
                );
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Preview_OneCandidateMatchesEveryPossibleRoll()
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            try
            {
                var a = new ItemStack(
                    data,
                    RolledStat.FromVector(StatVector.Of((StatType.AttackPct, 10)))
                );
                var b = new ItemStack(
                    data,
                    RolledStat.FromVector(StatVector.Of((StatType.AttackPct, 20)))
                );
                var rolled = CraftingService.RollCraftedStats(
                    RolledStat.ToVector(a.RolledStats),
                    RolledStat.ToVector(b.RolledStats),
                    new SystemRandomSource(1)
                );
                var result = new ItemStack(data, RolledStat.FromVector(rolled));
                Assert.AreEqual(
                    CraftParameterTextFormatter.Actual(StatType.AttackPct, result),
                    CraftParameterTextFormatter.Preview(StatType.AttackPct, a, b)
                );
                Assert.AreEqual(
                    "+0%",
                    CraftParameterTextFormatter.Preview(StatType.MaxHpPct, a, b)
                );
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void AuthoredPrefab_HasAllPresentationAndFlowReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab"
            );
            var layout = new SerializedObject(prefab.GetComponent<CraftUiLayout>());
            foreach (
                string name in new[]
                {
                    "_quickButton",
                    "_equipmentButton",
                    "_quickCount",
                    "_equipmentCount",
                    "_equipmentParameters",
                    "_quickParameters",
                    "_healValue",
                    "_predictionIcon",
                    "_predictionName",
                }
            )
                Assert.IsNotNull(layout.FindProperty(name).objectReferenceValue, name);
            var values = layout.FindProperty("_equipmentValues");
            Assert.AreEqual(5, values.arraySize);
            for (int i = 0; i < values.arraySize; i++)
                Assert.IsNotNull(values.GetArrayElementAtIndex(i).objectReferenceValue);
            var controller = new SerializedObject(prefab.GetComponent<CraftPanelController>());
            foreach (
                string name in new[]
                {
                    "_closeButton",
                    "_inventory",
                    "_craftButton",
                    "_craftedItemSlot",
                    "_resultItemImage",
                    "_resultItemName",
                    "_resultItemParameters",
                }
            )
                Assert.IsNotNull(controller.FindProperty(name).objectReferenceValue, name);
        }
    }
}
