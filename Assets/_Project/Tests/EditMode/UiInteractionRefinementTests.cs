using CreativeAI.Gameplay;
using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キャラクター画面の武器選択とインベントリの使うボタンの操作感の検証。
    /// 武器を選ぶと詳細とプレビューが更新され他の武器が暗くなる。使うボタンはダイアログなしで即回復・消費する。
    /// </summary>
    public class UiInteractionRefinementTests
    {
        [TestCase("CharacterUI/Prefabs/CharacterPanel.prefab")]
        [TestCase("Root/Prefabs/UIRoot.prefab")]
        public void HammerSelection_UpdatesDetailAndPreview_AndDimsOtherWeapons(string path)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Features/UI/" + path);
            try
            {
                var selector = root.GetComponentInChildren<CharacterWeaponSelector>(true);
                selector.Select(3);
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    "Assets/_Project/Features/Player/Data/Weapons/Hammer.asset"
                );
                Assert.AreEqual(
                    data.itemName,
                    selector.transform.Find("ViewArea/Name").GetComponent<TMP_Text>().text
                );
                Assert.AreSame(data, selector.SelectedWeapon);
                var preview = selector.transform.Find("ViewArea/WeaponView/WeaponPreview");
                Assert.False(
                    preview.GetComponent<Image>().enabled,
                    "The legacy sprite preview is replaced by 3D rendering"
                );
                Assert.NotNull(preview.GetComponentInChildren<ModelPreview>(true));
                Assert.NotNull(preview.GetComponentInChildren<RawImage>(true));
                Assert.AreEqual(
                    "+100%",
                    selector
                        .transform.Find("WeaponStatusArea/CRTD/TextArea/ValueText")
                        .GetComponent<TMP_Text>()
                        .text
                );
                var bar = selector.transform.parent.Find("WeaponChangeBar");
                Assert.Less(
                    bar.Find("BOW/WeaponIcon").GetComponent<Image>().color.r,
                    bar.Find("HAMMER/WeaponIcon").GetComponent<Image>().color.r
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void InventoryUseButton_HealsAndConsumesImmediately_WithoutOpeningDialog()
        {
            var root = PrefabUtility.LoadPrefabContents(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab"
            );
            var managerGo = new GameObject("TestInventoryManager");
            var playerGo = new GameObject("TestPlayer");
            var food = ScriptableObject.CreateInstance<FoodData>();
            var parameters = ScriptableObject.CreateInstance<PlayerParameterData>();
            try
            {
                parameters.baseMaxLife = 1000;
                var status = playerGo.AddComponent<PlayerStatus>();
                TestReflection.SetField(status, "_playerData", parameters);
                status.RestoreHp(100);
                var manager = managerGo.AddComponent<InventoryManager>();
                TestReflection.SetStaticProperty<InventoryManager>("Instance", manager);
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
                    root.GetComponentInChildren<ItemUseDialogPanel>(true).gameObject.activeSelf
                );
            }
            finally
            {
                TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
                Object.DestroyImmediate(managerGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(food);
                Object.DestroyImmediate(parameters);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
