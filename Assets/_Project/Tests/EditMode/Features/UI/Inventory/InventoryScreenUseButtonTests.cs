using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// インベントリ画面の「使う」ボタンの検証。
    /// 食材はその場で回復して1つ消費する。
    /// </summary>
    public class InventoryScreenUseButtonTests
    {
        private readonly TestResidents _residents = new();

        [Test]
        public void InventoryUseButton_HealsAndConsumesImmediately()
        {
            var root = PrefabUtility.LoadPrefabContents(
                "Assets/_Project/Features/UI/Inventory/Prefabs/InventoryPanel.prefab"
            );
            var playerGo = TestPlayer.Create();
            var food = ScriptableObject.CreateInstance<FoodData>();
            var parameters = ScriptableObject.CreateInstance<PlayerParameterData>();
            try
            {
                parameters.baseMaxLife = 1000;
                var status = playerGo.AddComponent<PlayerStatus>();
                TestReflection.SetField(status, "_playerData", parameters);
                status.RestoreHp(100);
                var manager = _residents.Add<InventoryManager>();
                manager.AddItem(food, 3);
                var stack = manager.GetAllItems().Find(item => item.Data == food);
                var screen = root.GetComponent<InventoryPanel>();
                var inventory = root.GetComponentInChildren<ItemListView>(true);
                TestReflection.SetField(inventory, "_selectedStack", stack);
                TestReflection.Invoke(screen, "UseSelected");
                Assert.AreEqual(2, stack.Count);
                Assert.AreEqual(300, status.CurrentHp, .01f);
            }
            finally
            {
                _residents.Clear();
                TestPlayer.Destroy(playerGo);
                Object.DestroyImmediate(food);
                Object.DestroyImmediate(parameters);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
