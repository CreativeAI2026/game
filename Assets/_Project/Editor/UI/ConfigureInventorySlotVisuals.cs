using System;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class ConfigureInventorySlotVisuals
    {
        private const string Art = "Assets/_Project/Art/UI/Common/Slot/";
        private static TestRunnerApi _runner;

        [MenuItem("Tools/CreativeAI/UI/Configure Inventory Slot Visuals %#&i")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            foreach (
                var path in new[]
                {
                    "Assets/_Project/Features/UI/InventoryUI/Prefabs/ItemSlot.prefab",
                    "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftListItemSlot.prefab",
                }
            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureSlot(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            foreach (
                var path in new[]
                {
                    "Assets/_Project/Features/UI/InventoryUI/Prefabs/ItemScrollView.prefab",
                    "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab",
                    "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab",
                }
            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var inventory in root.GetComponentsInChildren<InventoryView>(true))
                    {
                        var so = new SerializedObject(inventory);
                        var content = (Transform)so.FindProperty("_slotsRoot").objectReferenceValue;
                        var grid = content.GetComponent<GridLayoutGroup>();
                        grid.padding.top = 10;
                        grid.spacing = new Vector2(grid.spacing.x, 20);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[InventorySlotVisuals] Saved prefabs: top padding 10, spacing Y 20, occupied/empty frames and equipped check."
            );
        }

        public static void ConfigureSlot(GameObject root)
        {
            var frame = root.GetComponent<SlotFrameView>();
            var so = new SerializedObject(frame);
            Sprite Load(string name) =>
                AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png")
                ?? throw new InvalidOperationException("Missing sprite: " + name);
            so.FindProperty("_normalSprite").objectReferenceValue = Load("Slot_Item_Normal");
            so.FindProperty("_selectedSprite").objectReferenceValue = Load("Slot_Item_Selected");
            so.FindProperty("_itemWithCountSprite").objectReferenceValue = Load(
                "Slot_Item_WithCount"
            );
            so.FindProperty("_itemWithCountSelectedSprite").objectReferenceValue = Load(
                "Slot_Item_WithCount_Selected"
            );
            so.FindProperty("_role").enumValueIndex = (int)SlotFrameRole.Item;
            so.ApplyModifiedPropertiesWithoutUndo();
            var image = (Image)so.FindProperty("_frame").objectReferenceValue;
            image.sprite = Load("Slot_Item_Normal");
            image.preserveAspect = true;
            var marker = root.transform.Find("VisualRoot/EquippedMarker");
            var check = marker.GetComponent<Image>();
            check.sprite = Load("Slot_Equipped_Check");
            check.color = Color.white;
            check.preserveAspect = true;
            foreach (var text in marker.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
        }

        [MenuItem("Tools/CreativeAI/UI/Test Inventory Slot Frames %#&f")]
        public static void Test()
        {
            _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            _runner.RegisterCallbacks(new Callbacks());
            _runner.Execute(
                new ExecutionSettings(
                    new Filter
                    {
                        testMode = TestMode.EditMode,
                        testNames = new[]
                        {
                            "CreativeAI.Tests.EditMode.SlotFrameViewTests",
                            "CreativeAI.Tests.EditMode.FoodUseTests",
                            "CreativeAI.Tests.EditMode.ItemUseDialogPanelTests",
                            "CreativeAI.Tests.EditMode.InventoryViewKeyboardSubmitTests",
                            "CreativeAI.Tests.EditMode.ItemParameterListViewTests",
                            "CreativeAI.Tests.EditMode.UiInteractionRefinementTests",
                        },
                    }
                )
            );
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, "Temp/InventorySlotFrameTests.xml");
                Debug.Log(
                    $"[InventorySlotVisuals] Tests: {result.PassCount} passed, {result.FailCount} failed."
                );
            }
        }
    }
}
