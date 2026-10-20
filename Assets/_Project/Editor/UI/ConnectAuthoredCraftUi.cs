using System;
using System.Linq;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreativeAI.EditorTools
{
    public static class ConnectAuthoredCraftUi
    {
        private const string Path =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab";
        private static TestRunnerApi _testRunner;

        [MenuItem("Tools/CreativeAI/UI/Test Authored Craft UI %#&t")]
        public static void Test()
        {
            _testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            _testRunner.RegisterCallbacks(new TestCallbacks());
            _testRunner.Execute(
                new ExecutionSettings(
                    new Filter
                    {
                        testMode = TestMode.EditMode,
                        testNames = new[]
                        {
                            "CreativeAI.Tests.EditMode.CraftParameterTextFormatterTests",
                            "CreativeAI.Tests.EditMode.CraftingServiceTests",
                            "CreativeAI.Tests.EditMode.CraftFlowTests",
                            "CreativeAI.Tests.EditMode.CraftedStatRollTests",
                        },
                    }
                )
            );
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, "Temp/AuthoredCraftUiTests.xml");
                Debug.Log(
                    $"[AuthoredCraftUI] Tests: {result.PassCount} passed, {result.FailCount} failed."
                );
            }
        }

        [MenuItem("Tools/CreativeAI/UI/Show Craft Game View %#&g")]
        public static void ShowGame()
        {
            var gameType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            foreach (var old in Resources.FindObjectsOfTypeAll(gameType).OfType<EditorWindow>())
                old.Close();
            var factory = typeof(EditorWindow)
                .GetMethods()
                .First(m =>
                    m.Name == "GetWindow"
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 3
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.GetParameters()[1].ParameterType == typeof(bool)
                );
            var window = (EditorWindow)
                factory
                    .MakeGenericMethod(gameType)
                    .Invoke(null, new object[] { "Game", true, new[] { typeof(SceneView) } });
            window.Show();
            window.Focus();
            Debug.Log("[AuthoredCraftUI] Game view opened.");
        }

        [MenuItem("Tools/CreativeAI/UI/Connect Authored Craft UI %#&c")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                Transform Find(string name) =>
                    root.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == name)
                    ?? throw new InvalidOperationException($"Missing UI: {name}");
                void Set(Object target, string field, Object value)
                {
                    var so = new SerializedObject(target);
                    var property =
                        so.FindProperty(field) ?? throw new InvalidOperationException(field);
                    property.objectReferenceValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var controller = root.GetComponent<CraftPanelController>();
                var layout = root.GetComponent<CraftUiLayout>();
                var close = Find("CloseButton").GetComponent<Button>();
                if (close.GetComponent<HoverScaleOnPointer>() == null)
                    close.gameObject.AddComponent<HoverScaleOnPointer>();
                Set(controller, "_closeButton", close);
                Set(root.GetComponent<UIPanelStub>(), "_closeButton", close);

                var inventory = Find("Inventory").GetComponent<InventoryView>();
                Set(controller, "_inventory", inventory);
                Set(inventory, "_tabGroup", null);
                var tabs = Find("CraftCategoryTabs");
                tabs.GetComponent<TabGroup>().enabled = false;
                Set(tabs.GetComponent<TabGroup>(), "_tabButtonPrefab", null);
                foreach (var existing in tabs.GetComponents<LayoutGroup>())
                    Object.DestroyImmediate(existing);
                var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
                tabLayout.spacing = 12;
                tabLayout.childControlWidth = tabLayout.childControlHeight = true;
                tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
                var quick = tabs.Find("QUICK").GetComponent<Button>();
                var equipment = tabs.Find("EQUIP").GetComponent<Button>();
                Set(layout, "_quickButton", quick);
                Set(layout, "_equipmentButton", equipment);
                Set(
                    layout,
                    "_quickCount",
                    quick.transform.Find("Image/Number").GetComponent<TMP_Text>()
                );
                Set(
                    layout,
                    "_equipmentCount",
                    equipment.transform.Find("Image/Number").GetComponent<TMP_Text>()
                );

                // Repair the viewport's zero-size anchors in the authored instance.
                var scroll = inventory.GetComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.viewport.anchorMin = Vector2.zero;
                scroll.viewport.anchorMax = Vector2.one;
                scroll.viewport.offsetMin = Vector2.zero;
                scroll.viewport.offsetMax = new Vector2(-20, 0);
                var inventoryLayout =
                    inventory.GetComponent<LayoutElement>()
                    ?? inventory.gameObject.AddComponent<LayoutElement>();
                inventoryLayout.flexibleHeight = 1;
                inventoryLayout.minHeight = 100;

                var equipPattern = Find("EquipPattern");
                var quickPattern = Find("QuickPattern");
                var equipRect = (RectTransform)equipPattern;
                equipRect.anchorMin = Vector2.zero;
                equipRect.anchorMax = Vector2.one;
                equipRect.offsetMin = equipRect.offsetMax = Vector2.zero;
                var rows = equipPattern
                    .Cast<Transform>()
                    .Where(t => t.Find("Value") != null)
                    .ToArray();
                var labels = new[] { "攻撃", "防御", "会心率", "会心ダメージ", "最大HP" };
                if (rows.Length != labels.Length)
                    throw new InvalidOperationException("Expected five stat rows.");
                for (int i = 0; i < rows.Length; i++)
                {
                    rows[i].Find("Label").GetComponent<TMP_Text>().text = labels[i];
                    rows[i].Find("Value").GetComponent<TMP_Text>().text = "—";
                }
                var quickRow = quickPattern.GetChild(0);
                quickRow.Find("Label").GetComponent<TMP_Text>().text = "回復量";
                quickRow.Find("Value").GetComponent<TMP_Text>().text = "—";
                var quickRect = (RectTransform)quickRow;
                quickRect.anchorMin = new Vector2(0, 1);
                quickRect.anchorMax = Vector2.one;
                quickRect.pivot = new Vector2(.5f, 1);
                quickRect.anchoredPosition = Vector2.zero;
                quickRect.sizeDelta = new Vector2(0, 80);
                var quickRowLayout = quickRow.GetComponent<LayoutElement>();
                if (quickRowLayout != null)
                    quickRowLayout.ignoreLayout = true;
                Set(layout, "_quickParameters", quickPattern.gameObject);
                Set(layout, "_equipmentParameters", equipPattern.gameObject);
                Set(layout, "_healValue", quickRow.Find("Value").GetComponent<TMP_Text>());
                var layoutSo = new SerializedObject(layout);
                var values = layoutSo.FindProperty("_equipmentValues");
                values.arraySize = rows.Length;
                for (int i = 0; i < rows.Length; i++)
                    values.GetArrayElementAtIndex(i).objectReferenceValue = rows[i]
                        .Find("Value")
                        .GetComponent<TMP_Text>();
                layoutSo.ApplyModifiedPropertiesWithoutUndo();
                equipPattern.gameObject.SetActive(false);
                quickPattern.gameObject.SetActive(true);

                // Keep all additions in the prefab, including the finished-item popup.
                var predictionName = Find("Name").GetComponent<TMP_Text>();
                var predictionIcon = Find("PreviewIcon").GetComponent<Image>();
                Set(layout, "_predictionName", predictionName);
                Set(layout, "_predictionIcon", predictionIcon);
                predictionIcon.preserveAspect = true;
                var result = Find("ResultPanel");
                var resultIcon = result.Find("ResultIcon");
                if (resultIcon == null)
                {
                    resultIcon = new GameObject(
                        "ResultIcon",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image)
                    ).transform;
                    resultIcon.SetParent(result, false);
                }
                var resultImage = resultIcon.GetComponent<Image>();
                resultImage.preserveAspect = true;
                resultImage.raycastTarget = false;
                Position((RectTransform)resultIcon, new Vector2(0, 130), new Vector2(260, 260));
                var resultName = result.Find("ResultName");
                if (resultName == null)
                {
                    resultName = Object.Instantiate(predictionName.gameObject, result).transform;
                    resultName.name = "ResultName";
                    foreach (var fitter in resultName.GetComponents<ContentSizeFitter>())
                        Object.DestroyImmediate(fitter);
                }
                Position((RectTransform)resultName, new Vector2(0, -45), new Vector2(1000, 70));
                var nameText = resultName.GetComponent<TMP_Text>();
                nameText.fontSize = 42;
                nameText.alignment = TextAlignmentOptions.Center;
                var parameters = Find("ItemParameters").GetComponent<TMP_Text>();
                parameters.transform.SetParent(result, false);
                Position(parameters.rectTransform, new Vector2(0, -230), new Vector2(850, 250));
                parameters.fontSize = 34;
                parameters.alignment = TextAlignmentOptions.Center;
                Set(controller, "_resultItemImage", resultImage);
                Set(controller, "_resultItemName", nameText);
                Set(controller, "_resultItemParameters", parameters);
                var resultTitle = result.Find("Title").GetComponent<TMP_Text>();
                resultTitle.fontSize = 64;
                resultTitle.alignment = TextAlignmentOptions.Center;
                Position(resultTitle.rectTransform, new Vector2(0, 370), new Vector2(1000, 100));
                foreach (var material in root.GetComponentsInChildren<MaterialSlot>(true))
                {
                    var hint = material
                        .GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == "MaterialHint");
                    Set(material, "_materialHint", hint != null ? hint.gameObject : null);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Path);
                Debug.Log(
                    "[AuthoredCraftUI] Prefab connected: category buttons, five equipment stats, recovery, close button and craft result."
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Position(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
