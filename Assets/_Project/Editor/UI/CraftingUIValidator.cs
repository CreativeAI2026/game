using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using CreativeAI.UI.Common;
using CreativeAI.UI.CraftingUI;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    public static class CraftingUIValidator
    {
        private const string FieldArea01Path = "Assets/_Project/Scenes/Field/Field_Area01.unity";
        private const string RecipeSlotPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/RecipeSlot.prefab";

        [MenuItem("Tools/CreativeAI/UI/Validate Crafting UI")]
        public static void ValidateFromMenu()
        {
            var report = new UIValidationReport("Crafting UI");
            Scene scene = SceneManager.GetSceneByPath(FieldArea01Path);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (openedForValidation)
                    scene = EditorSceneManager.OpenScene(FieldArea01Path, OpenSceneMode.Additive);

                ValidateScene(scene, report);
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
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            report.Complete();
        }

        public static void ValidateAllFromCommandLine()
        {
            ValidateFromMenu();
        }

        private static void ValidateScene(Scene scene, UIValidationReport report)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Error(FieldArea01Path, "Scene", "Sceneを読み込めません。", null);
                return;
            }

            var craftPanels = FindAll<CraftPanel>(scene);
            var freeCraftPanels = FindAll<FreeCraftPanelController>(scene);
            var quantityDialogs = FindAll<CraftQuantityDialog>(scene);
            var recipeCraftPanels = FindAll<RecipeCraftPanel>(scene);

            ValidateExpectedCount(craftPanels, nameof(CraftPanel), report);
            ValidateExpectedCount(freeCraftPanels, nameof(FreeCraftPanelController), report);
            ValidateExpectedCount(quantityDialogs, nameof(CraftQuantityDialog), report);
            ValidateExpectedCount(recipeCraftPanels, nameof(RecipeCraftPanel), report);

            foreach (var craftPanel in craftPanels)
                ValidateCraftPanel(craftPanel, report);
            foreach (var freeCraftPanel in freeCraftPanels)
                ValidateFreeCraftPanel(freeCraftPanel, report);
            foreach (var quantityDialog in quantityDialogs)
                ValidateQuantityDialog(quantityDialog, report);
            foreach (var recipeCraftPanel in recipeCraftPanels)
                ValidateRecipeCraftPanel(recipeCraftPanel, report);
        }

        private static void ValidateFreeCraftPanel(
            FreeCraftPanelController panel,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_craftPanel",
                "_inventory",
                "_slotsRoot",
                "_detailPanel",
                "_craftButton",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedPanel = new SerializedObject(panel);
            var slotsRoot = GetReference<Transform>(serializedPanel, "_slotsRoot");
            if (slotsRoot == null)
                return;

            int materialSlotCount = 0;
            for (int i = 0; i < slotsRoot.childCount; i++)
            {
                var child = slotsRoot.GetChild(i);
                if (child.GetComponent<MaterialSlot>() != null)
                {
                    materialSlotCount++;
                    continue;
                }

                report.Error(
                    UIHierarchyPathUtility.GetPath(child),
                    nameof(MaterialSlot),
                    "MaterialSlotsRoot直下の要素にはMaterialSlotが必要です。",
                    child
                );
            }

            if (materialSlotCount == 0)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(slotsRoot),
                    nameof(MaterialSlot),
                    "FreeCraftには1個以上のMaterialSlotが必要です。",
                    slotsRoot
                );
            }
        }

        private static void ValidateCraftPanel(CraftPanel panel, UIValidationReport report)
        {
            string[] requiredFields =
            {
                "_recipeDB",
                "_loadingPanel",
                "_loadingGear",
                "_resultPanel",
                "_resultPanelBackground",
                "_resultPanelTitle",
                "_resultItemImage",
                "_resultItemName",
                "_closeButton",
                "_closeButtonButton",
                "_warningText",
                "_warningCanvasGroup",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedObject = new SerializedObject(panel);
            var warningText = GetReference<TMP_Text>(serializedObject, "_warningText");
            var warningCanvasGroup = GetReference<CanvasGroup>(
                serializedObject,
                "_warningCanvasGroup"
            );
            if (
                warningText != null
                && warningCanvasGroup != null
                && warningText.gameObject != warningCanvasGroup.gameObject
            )
            {
                report.Error(
                    panel.name,
                    "_warningCanvasGroup",
                    "_warningTextと同じGameObjectのCanvasGroupを設定してください。",
                    panel
                );
            }

            ValidateWarningText(warningText, report);

            var resultPanel = GetReference<GameObject>(serializedObject, "_resultPanel");
            var resultPanelBackground = GetReference<GameObject>(
                serializedObject,
                "_resultPanelBackground"
            );
            ValidateResultPanel(resultPanel, report);
            if (resultPanelBackground != null)
                ValidateRaycastGraphic(resultPanelBackground, "ResultPanel背景", report);
        }

        private static void ValidateWarningText(TMP_Text warningText, UIValidationReport report)
        {
            if (warningText == null)
                return;

            if (warningText.rectTransform == null)
            {
                report.Error(
                    warningText.name,
                    nameof(RectTransform),
                    "Warning TextからRectTransformを取得できません。TMP_Text参照を確認してください。",
                    warningText
                );
            }

            if (warningText.GetComponent<CanvasGroup>() == null)
            {
                report.Error(
                    warningText.name,
                    nameof(CanvasGroup),
                    "WarningTextにはフェード制御用のCanvasGroupが必要です。",
                    warningText
                );
            }

            if (warningText.raycastTarget)
            {
                report.Error(
                    warningText.name,
                    "Raycast Target",
                    "一時通知のWarningTextはRaycast TargetをOFFにしてください。",
                    warningText
                );
            }

            for (
                Transform parent = warningText.transform.parent;
                parent != null;
                parent = parent.parent
            )
            {
                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    report.Error(
                        warningText.name,
                        "Hierarchy",
                        $"WarningTextをLayoutGroup '{parent.name}' の配下に置かないでください。",
                        warningText
                    );
                    break;
                }

                if (parent.GetComponent<ScrollRect>() != null)
                {
                    report.Error(
                        warningText.name,
                        "Hierarchy",
                        $"WarningTextをScrollRect '{parent.name}' の配下に置かないでください。",
                        warningText
                    );
                    break;
                }

                if (
                    parent.name.Contains("SlotRoot", StringComparison.OrdinalIgnoreCase)
                    || parent.name.Equals("Content", StringComparison.OrdinalIgnoreCase)
                )
                {
                    report.Error(
                        warningText.name,
                        "Hierarchy",
                        $"WarningTextを通常コンテンツ用Root '{parent.name}' の配下に置かないでください。",
                        warningText
                    );
                    break;
                }
            }
        }

        private static void ValidateResultPanel(GameObject resultPanel, UIValidationReport report)
        {
            if (resultPanel == null)
                return;

            if (resultPanel.GetComponent<CanvasGroup>() == null)
            {
                report.Error(
                    resultPanel.name,
                    nameof(CanvasGroup),
                    "ResultPanelには表示・非表示Tween用のCanvasGroupが必要です。",
                    resultPanel
                );
            }

            var catchers = resultPanel.GetComponents<CloseOnSelfClick>();
            if (catchers.Length != 1)
            {
                report.Error(
                    resultPanel.name,
                    nameof(CloseOnSelfClick),
                    $"ResultPanel Rootに1個必要です。現在: {catchers.Length}個",
                    resultPanel
                );
                return;
            }

            var serializedCatcher = new SerializedObject(catchers[0]);
            var target = GetReference<GameObject>(serializedCatcher, "_targetToHide");
            if (target != null)
            {
                report.Error(
                    resultPanel.name,
                    "Target To Hide",
                    "ResultPanelはHideSharedResult()経由で閉じるため、CloseOnSelfClick.TargetToHideは使用しないでください。",
                    catchers[0]
                );
            }
            else
            {
                report.Ok(
                    resultPanel.name,
                    "Target To Hide",
                    "Noneです。Runtime actionからHideSharedResult()を使用します。",
                    catchers[0]
                );
            }
        }

        private static void ValidateQuantityDialog(
            CraftQuantityDialog dialog,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_panelRoot",
                "_dialogRoot",
                "_itemImage",
                "_itemName",
                "_countLabel",
                "_inputField",
                "_inputText",
                "_minButton",
                "_minusButton",
                "_plusButton",
                "_maxButton",
                "_craftButton",
                "_craftButtonText",
                "_dialogCanvasGroup",
                "_outsideClickCatcher",
            };
            ValidateRequiredReferences(dialog, requiredFields, report);

            var serializedDialog = new SerializedObject(dialog);
            var panelRoot = GetReference<GameObject>(serializedDialog, "_panelRoot");
            var dialogRoot = GetReference<GameObject>(serializedDialog, "_dialogRoot");
            var catcher = GetReference<CloseOnSelfClick>(serializedDialog, "_outsideClickCatcher");

            if (panelRoot != null)
                ValidateRaycastGraphic(panelRoot, "CQD背景Root", report);
            if (dialogRoot != null)
                ValidateRaycastGraphic(dialogRoot, "DialogRoot", report);

            if (catcher == null)
                return;

            if (panelRoot == null || catcher.gameObject != panelRoot)
            {
                report.Error(
                    dialog.name,
                    "_outsideClickCatcher",
                    "CQD-Panel Root上のCloseOnSelfClickを設定してください。",
                    dialog
                );
            }

            ValidateCloseOnSelfClickUsesRuntimeHide(catcher, report);
        }

        private static void ValidateCloseOnSelfClickUsesRuntimeHide(
            CloseOnSelfClick catcher,
            UIValidationReport report
        )
        {
            var serializedCatcher = new SerializedObject(catcher);
            var target = GetReference<GameObject>(serializedCatcher, "_targetToHide");
            if (target != null)
            {
                report.Error(
                    catcher.name,
                    "Target To Hide",
                    "Noneにしてください。CQDはCraftQuantityDialog.Hide()経由で閉じます。",
                    catcher
                );
            }
            else
            {
                report.Ok(
                    catcher.name,
                    "Target To Hide",
                    "Noneです。コード側のHide()登録を使用できます。",
                    catcher
                );
            }

            int persistentCallCount = GetPersistentCallCount(serializedCatcher);
            if (persistentCallCount > 0)
            {
                report.Error(
                    catcher.name,
                    "On Self Click",
                    $"Persistent Listenerが{persistentCallCount}件あります。空にしてコード側登録だけにしてください。",
                    catcher
                );
            }
            else
            {
                report.Ok(
                    catcher.name,
                    "On Self Click",
                    "Persistent Listenerは空です。二重登録はありません。",
                    catcher
                );
            }
        }

        private static void ValidateRecipeCraftPanel(
            RecipeCraftPanel panel,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_recipeDB",
                "_craftPanel",
                "_recipeSlotPrefab",
                "_recipeList",
                "_recipeContent",
                "_categoryTabGroup",
                "_detailPanel",
                "_materialList",
                "_quantityDialogPanel",
                "_quantityDialog",
                "_quantityDialogController",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var materialRows = new SerializedObject(panel).FindProperty("_materialRows");
            if (materialRows == null || materialRows.arraySize == 0)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    "_materialRows",
                    "RecipeCraftで使用するRecipeMaterialRowをInspectorで設定してください。",
                    panel
                );
            }
            else
            {
                for (int i = 0; i < materialRows.arraySize; i++)
                {
                    if (materialRows.GetArrayElementAtIndex(i).objectReferenceValue != null)
                        continue;

                    report.Error(
                        UIHierarchyPathUtility.GetPath(panel.transform),
                        $"_materialRows[{i}]",
                        "RecipeMaterialRow参照を設定してください。",
                        panel
                    );
                }
            }

            var serializedPanel = new SerializedObject(panel);
            ValidateRecipeCategoryTabGroup(panel, serializedPanel, report);
            var recipeSlotPrefab = GetReference<GameObject>(serializedPanel, "_recipeSlotPrefab");
            if (recipeSlotPrefab == null)
                return;

            string path = AssetDatabase.GetAssetPath(recipeSlotPrefab);
            if (path != RecipeSlotPath || recipeSlotPrefab.GetComponent<RecipeSlot>() == null)
            {
                report.Error(
                    panel.name,
                    "_recipeSlotPrefab",
                    $"'{RecipeSlotPath}' のRecipeSlot Variantを設定してください。現在: '{path}'",
                    panel
                );
            }
            else
            {
                report.Ok(
                    panel.name,
                    "_recipeSlotPrefab",
                    "正しいRecipeSlot Variantを参照しています。",
                    panel
                );
            }
        }

        private static void ValidateRecipeCategoryTabGroup(
            RecipeCraftPanel panel,
            SerializedObject serializedPanel,
            UIValidationReport report
        )
        {
            var tabGroup = GetReference<TabGroup>(serializedPanel, "_categoryTabGroup");
            if (tabGroup == null)
                return;

            ItemCategory[] expectedCategories = { ItemCategory.Equipment, ItemCategory.Food };
            var actualCategories = new List<ItemCategory>();
            bool referencesRecipeCraftView = false;

            for (int i = 0; i < tabGroup.EntryCount; i++)
            {
                var view = tabGroup.GetView(i);
                if (
                    view != null
                    && (
                        panel.transform == view.transform
                        || panel.transform.IsChildOf(view.transform)
                    )
                )
                {
                    referencesRecipeCraftView = true;
                }

                var definition = tabGroup.GetDefinitionForEntry(i);
                if (definition is not InventoryTabDefinition inventoryDefinition)
                {
                    report.Error(
                        tabGroup.name,
                        $"TabEntry[{i}].definition",
                        $"Recipe category tabには{nameof(InventoryTabDefinition)}を設定してください。",
                        tabGroup
                    );
                    continue;
                }

                actualCategories.Add(inventoryDefinition.Category);
            }

            if (referencesRecipeCraftView)
            {
                report.Error(
                    panel.name,
                    "_categoryTabGroup",
                    "FreeCraft / RecipeCraft画面切替用TabGroupをカテゴリ用として参照しています。RecipeCraftPanel内のカテゴリTabGroupを設定してください。",
                    panel
                );
            }

            if (!actualCategories.SequenceEqual(expectedCategories))
            {
                report.Error(
                    tabGroup.name,
                    "Recipe category order",
                    $"カテゴリを次の順に設定してください: {string.Join(" / ", expectedCategories)}",
                    tabGroup
                );
            }
            else
            {
                report.Ok(
                    tabGroup.name,
                    "Recipe category order",
                    $"カテゴリ順が正しいです: {string.Join(" / ", actualCategories)}",
                    tabGroup
                );
            }
        }

        private static void ValidateRaycastGraphic(
            GameObject target,
            string fieldName,
            UIValidationReport report
        )
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                report.Error(
                    target.name,
                    fieldName,
                    "Graphicを設定し、内側クリックが背景へ貫通しないようにしてください。",
                    target
                );
            }
            else if (!graphic.raycastTarget)
            {
                report.Error(
                    target.name,
                    fieldName,
                    "GraphicのRaycast TargetをONにしてください。",
                    target
                );
            }
            else
            {
                report.Ok(target.name, fieldName, "Raycast Targetが有効です。", target);
            }
        }

        private static void ValidateRequiredReferences(
            Component owner,
            IEnumerable<string> fieldNames,
            UIValidationReport report
        )
        {
            var serializedObject = new SerializedObject(owner);
            foreach (string fieldName in fieldNames)
            {
                var property = serializedObject.FindProperty(fieldName);
                if (property == null)
                {
                    report.Error(
                        UIHierarchyPathUtility.GetPath(owner.transform),
                        fieldName,
                        "SerializeFieldが見つかりません。Validatorの定義を更新してください。",
                        owner
                    );
                }
                else if (property.objectReferenceValue == null)
                {
                    report.Error(
                        UIHierarchyPathUtility.GetPath(owner.transform),
                        fieldName,
                        "Inspectorで必須参照を設定してください。",
                        owner
                    );
                }
                else
                {
                    report.Ok(
                        UIHierarchyPathUtility.GetPath(owner.transform),
                        fieldName,
                        $"{property.objectReferenceValue.name} を参照しています。",
                        owner
                    );
                }
            }
        }

        private static T GetReference<T>(SerializedObject serializedObject, string fieldName)
            where T : UnityEngine.Object
        {
            return serializedObject.FindProperty(fieldName)?.objectReferenceValue as T;
        }

        private static int GetPersistentCallCount(SerializedObject serializedObject)
        {
            var unityEvent = serializedObject.FindProperty("_onSelfClick");
            var persistentCalls = unityEvent?.FindPropertyRelative("m_PersistentCalls");
            var calls = persistentCalls?.FindPropertyRelative("m_Calls");
            return calls?.arraySize ?? 0;
        }

        private static T[] FindAll<T>(Scene scene)
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void ValidateExpectedCount<T>(
            IReadOnlyCollection<T> components,
            string componentName,
            UIValidationReport report,
            bool allowMultiple = false
        )
            where T : Component
        {
            bool valid = allowMultiple ? components.Count > 0 : components.Count == 1;
            if (valid)
            {
                report.Ok(
                    FieldArea01Path,
                    componentName,
                    $"Scene内に{components.Count}個あります。",
                    components.FirstOrDefault()
                );
                return;
            }

            report.Error(
                FieldArea01Path,
                componentName,
                allowMultiple
                    ? "Scene内に1個以上必要です。"
                    : $"Scene内に1個必要です。現在: {components.Count}個",
                components.FirstOrDefault()
            );
        }
    }
}
