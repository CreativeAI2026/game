using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// インベントリ画面の「使う」ボタンの検証。
    /// 食材は確認ダイアログを出さずに、その場で回復して1つ消費する。
    /// </summary>
    public class InventoryScreenUseButtonTests
    {
        private readonly TestResidents _residents = new();

        [Test]
        public void InventoryUseButton_HealsAndConsumesImmediately_WithoutOpeningDialog()
        {
            var root = PrefabUtility.LoadPrefabContents(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab"
            );
            var playerGo = new GameObject("TestPlayer");
            var food = ScriptableObject.CreateInstance<FoodData>();
            var parameters = ScriptableObject.CreateInstance<PlayerParameterData>();
            try
            {
                parameters.baseMaxLife = 1000;
                var status = playerGo.AddComponent<PlayerStatus>();
                TestReflection.SetField(status, "_playerData", parameters);
                status.RestoreHp(100);
                var manager = _residents.Add<InventoryManager>();
                TestReflection.SetField(manager, "_playerStatus", status);
                manager.AddItem(food, 3);
                var stack = manager.GetAllItems().Find(item => item.Data == food);
                var screen = root.GetComponent<InventoryScreenView>();
                var inventory = root.GetComponentInChildren<InventoryView>(true);
                TestReflection.SetField(inventory, "_selectedStack", stack);
                TestReflection.Invoke(screen, "UseSelected");
                Assert.AreEqual(2, stack.Count);
                Assert.AreEqual(300, status.CurrentHp, .01f);
                Assert.IsFalse(
                    root.GetComponentInChildren<ItemUseDialogPanel>(true).gameObject.activeSelf,
                    "確認ダイアログは開かない"
                );
            }
            finally
            {
                _residents.Clear();
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(food);
                Object.DestroyImmediate(parameters);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
