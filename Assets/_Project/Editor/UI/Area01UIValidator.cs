using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    public static class Area01UIValidator
    {
        private const string UIRootPrefabPath =
            "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string FieldArea01Path = "Assets/_Project/Scenes/UI/UI_Sandbox.unity";
        private const string ResidentBootstrapConfigPath =
            "Assets/_Project/Resources/ResidentBootstrapConfig.asset";
        private const int ExpectedInventoryCount = 4;

        [MenuItem("Tools/CreativeAI/UI/Validate Area01 UI")]
        public static void ValidateFromMenu()
        {
            SlotPrefabValidator.ValidateFromMenu();
            CraftingUIValidator.ValidateFromMenu();
            InventoryUIValidator.ValidateFromMenu();
            ValidateArea01Connections();
        }

        [MenuItem("Tools/CreativeAI/UI/Validate All UI")]
        public static void ValidateAllFromMenu()
        {
            ValidateFromMenu();
        }

        private static void ValidateArea01Connections()
        {
            var report = new UIValidationReport("Area01 UI Connections");
            GameObject root = null;
            Scene fieldScene = default;
            bool openedFieldScene = false;

            try
            {
                root = PrefabUtility.LoadPrefabContents(UIRootPrefabPath);
                ValidateScene(root.scene, report);

                fieldScene = SceneManager.GetSceneByPath(FieldArea01Path);
                openedFieldScene = !fieldScene.IsValid() || !fieldScene.isLoaded;
                if (openedFieldScene)
                    fieldScene = EditorSceneManager.OpenScene(
                        FieldArea01Path,
                        OpenSceneMode.Additive
                    );
                ValidateFieldScene(fieldScene, report);
            }
            catch (Exception exception)
            {
                report.Error(
                    FieldArea01Path,
                    "Scene",
                    $"Scene検査中に例外が発生しました: {exception.Message}",
                    null
                );
                Debug.LogException(exception);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
                if (openedFieldScene && fieldScene.IsValid() && fieldScene.isLoaded)
                    EditorSceneManager.CloseScene(fieldScene, true);
            }

            report.Complete();
        }

        private static void ValidateScene(Scene scene, UIValidationReport report)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Error(
                    FieldArea01Path,
                    "Scene",
                    "Resident UIRoot Prefabを検査対象として読み込めませんでした。",
                    null
                );
                return;
            }

            var inventories = FindAll<InventoryView>(scene);
            var panelControllers = FindAll<InventoryPanelController>(scene);
            var craftControllers = FindAll<CraftPanelController>(scene);
            var tabGroups = FindAll<TabGroup>(scene);

            ValidateCount(inventories, ExpectedInventoryCount, nameof(InventoryView), report);
            ValidateCount(panelControllers, 1, nameof(InventoryPanelController), report);
            ValidateCount(craftControllers, 1, nameof(CraftPanelController), report);
            ValidateAllTabDefinitions(tabGroups, report);
            ValidateViewSwitchTabGroups(tabGroups, inventories, report);

            var providers = inventories.ToDictionary(
                inventory => inventory,
                _ => new List<string>()
            );

            foreach (var controller in panelControllers)
                AddProvider(controller, "_inventory", nameof(InventoryPanelController));
            foreach (var controller in craftControllers)
                AddProvider(controller, "_inventory", nameof(CraftPanelController));

            foreach (var inventory in inventories)
                ValidateSingleProvider(inventory, providers[inventory], report);

            void AddProvider(MonoBehaviour controller, string fieldName, string providerName)
            {
                var serializedController = new SerializedObject(controller);
                var inventory = GetReference<InventoryView>(serializedController, fieldName);
                if (
                    inventory == null
                    || !providers.TryGetValue(inventory, out var inventoryProviders)
                )
                    return;

                inventoryProviders.Add(providerName);
                ValidateProviderHierarchy(controller, inventory, providerName, report);
                ItemCategory[] expectedCategories =
                    controller is CraftPanelController
                        ? new[] { ItemCategory.Equipment, ItemCategory.Food }
                        : new[]
                        {
                            // 武器はインベントリ管理外。インベントリのタブは 装備品/食材/大事なもの の3つ。
                            ItemCategory.Equipment,
                            ItemCategory.Food,
                            ItemCategory.Important,
                        };
                ValidateTabProvider(inventory, expectedCategories, report);
            }
        }

        private static void ValidateViewSwitchTabGroups(
            IEnumerable<TabGroup> tabGroups,
            IEnumerable<InventoryView> inventories,
            UIValidationReport report
        )
        {
            var categoryTabGroups = new HashSet<TabGroup>();
            foreach (var inventory in inventories)
            {
                var serializedInventory = new SerializedObject(inventory);
                var tabGroup = GetReference<TabGroup>(serializedInventory, "_tabGroup");
                if (tabGroup != null)
                    categoryTabGroups.Add(tabGroup);
            }

            foreach (var tabGroup in tabGroups)
            {
                if (categoryTabGroups.Contains(tabGroup))
                    continue;

                for (int i = 0; i < tabGroup.EntryCount; i++)
                {
                    if (tabGroup.GetView(i) != null)
                        continue;

                    report.Error(
                        UIHierarchyPathUtility.GetPath(tabGroup.transform),
                        $"TabEntry[{i}].view",
                        "View切替用TabGroupにはEntry.viewを設定してください。カテゴリ通知専用TabGroupだけview未設定を許容します。",
                        tabGroup
                    );
                }
            }
        }

        private static void ValidateAllTabDefinitions(
            IEnumerable<TabGroup> tabGroups,
            UIValidationReport report
        )
        {
            foreach (var tabGroup in tabGroups)
            {
                for (int i = 0; i < tabGroup.EntryCount; i++)
                {
                    if (tabGroup.GetDefinitionForEntry(i) != null)
                        continue;

                    report.Error(
                        tabGroup.name,
                        $"TabEntry[{i}].definition",
                        "TabDefinitionを設定してください。不要なタブはEntryから除外してください。",
                        tabGroup
                    );
                }
            }
        }

        private static void ValidateProviderHierarchy(
            MonoBehaviour controller,
            InventoryView inventory,
            string providerName,
            UIValidationReport report
        )
        {
            bool belongsToController =
                inventory.transform == controller.transform
                || inventory.transform.IsChildOf(controller.transform);
            if (belongsToController)
            {
                report.Ok(
                    controller.name,
                    "_inventory",
                    $"{providerName}配下のInventoryを参照しています。",
                    controller
                );
            }
            else
            {
                report.Error(
                    controller.name,
                    "_inventory",
                    $"{providerName}自身の階層内にあるInventoryを設定してください。現在: {inventory.name}",
                    controller
                );
            }
        }

        private static void ValidateTabProvider(
            InventoryView inventory,
            IReadOnlyList<ItemCategory> expectedCategories,
            UIValidationReport report
        )
        {
            var serializedInventory = new SerializedObject(inventory);
            var tabGroup = GetReference<TabGroup>(serializedInventory, "_tabGroup");
            if (tabGroup == null)
            {
                report.Error(
                    inventory.name,
                    "_tabGroup",
                    "通常Inventoryと調合画面にはTabGroupを設定してください。",
                    inventory
                );
                return;
            }

            var actualCategories = new List<ItemCategory>();
            for (int i = 0; i < tabGroup.EntryCount; i++)
            {
                var definition = tabGroup.GetDefinitionForEntry(i);
                if (definition is not InventoryTabDefinition inventoryDefinition)
                {
                    report.Error(
                        tabGroup.name,
                        $"TabEntry[{i}].definition",
                        $"{nameof(InventoryTabDefinition)}を設定してください。",
                        tabGroup
                    );
                    continue;
                }

                actualCategories.Add(inventoryDefinition.Category);
                report.Ok(
                    tabGroup.name,
                    $"TabEntry[{i}].definition",
                    $"{inventoryDefinition.name} → {inventoryDefinition.Category}",
                    inventoryDefinition
                );
            }

            if (actualCategories.SequenceEqual(expectedCategories))
            {
                report.Ok(
                    tabGroup.name,
                    "TabEntry definitions",
                    $"カテゴリ順が正しいです: {string.Join(" / ", actualCategories)}",
                    tabGroup
                );
            }
            else
            {
                report.Error(
                    tabGroup.name,
                    "TabEntry definitions",
                    $"InventoryTabDefinitionを次の順に設定してください: {string.Join(" / ", expectedCategories)}",
                    tabGroup
                );
            }
        }

        private static void ValidateSingleProvider(
            InventoryView inventory,
            IReadOnlyCollection<string> providers,
            UIValidationReport report
        )
        {
            if (providers.Count == 1)
            {
                report.Ok(
                    inventory.name,
                    "ItemsRequested provider",
                    $"供給元は{providers.First()}の1つです。",
                    inventory
                );
            }
            else
            {
                report.Error(
                    inventory.name,
                    "ItemsRequested provider",
                    providers.Count == 0
                        ? "供給元Controllerがありません。対応するControllerの_inventoryを設定してください。"
                        : $"供給元が重複しています: {string.Join(", ", providers)}",
                    inventory
                );
            }
        }

        private static void ValidateFieldScene(Scene scene, UIValidationReport report)
        {
            if (
                !scene.IsValid()
                || !scene.isLoaded
                || !string.Equals(scene.path, FieldArea01Path, StringComparison.Ordinal)
            )
            {
                report.Error(
                    FieldArea01Path,
                    "Scene",
                    "Area01開発Sceneを検査対象として読み込めませんでした。",
                    null
                );
                return;
            }

            var bootstraps = FindAll<FieldDevBootstrap>(scene);
            ValidateCount(bootstraps, 1, nameof(FieldDevBootstrap), report);

            var sceneRoots = FindAll<UIRoot>(scene);
            ValidateCount(sceneRoots, 0, nameof(UIRoot), report);

            var config = AssetDatabase.LoadAssetAtPath<ResidentBootstrapConfig>(
                ResidentBootstrapConfigPath
            );
            if (config == null)
            {
                report.Error(
                    ResidentBootstrapConfigPath,
                    nameof(ResidentBootstrapConfig),
                    "ResidentBootstrapConfig Assetが見つかりません。",
                    null
                );
                return;
            }

            string configuredPrefabPath = AssetDatabase.GetAssetPath(config.uiRootPrefab);
            if (configuredPrefabPath == UIRootPrefabPath)
            {
                report.Ok(
                    ResidentBootstrapConfigPath,
                    "uiRootPrefab",
                    "Resident UIRoot Prefabを参照しています。",
                    config
                );
            }
            else
            {
                report.Error(
                    ResidentBootstrapConfigPath,
                    "uiRootPrefab",
                    $"'{UIRootPrefabPath}'を設定してください。現在: '{configuredPrefabPath}'",
                    config
                );
            }
        }

        private static void ValidateCount<T>(
            IReadOnlyCollection<T> components,
            int expectedCount,
            string componentName,
            UIValidationReport report
        )
            where T : Component
        {
            if (components.Count == expectedCount)
            {
                report.Ok(
                    FieldArea01Path,
                    componentName,
                    $"期待数{expectedCount}件と一致しています。",
                    components.FirstOrDefault()
                );
            }
            else
            {
                report.Error(
                    FieldArea01Path,
                    componentName,
                    $"{expectedCount}件必要です。現在: {components.Count}件",
                    components.FirstOrDefault()
                );
            }
        }

        private static T GetReference<T>(SerializedObject serializedObject, string fieldName)
            where T : UnityEngine.Object
        {
            return serializedObject.FindProperty(fieldName)?.objectReferenceValue as T;
        }

        private static T[] FindAll<T>(Scene scene)
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
