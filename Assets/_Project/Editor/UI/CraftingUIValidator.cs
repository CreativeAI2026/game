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
        private const string FieldArea01Path = "Assets/_Project/Scenes/UI/UI_Sandbox.unity";
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

            var craftPanels = FindAll<CraftPanelController>(scene);
            var freeCraftPanels = FindAll<FreeCraftPanelController>(scene);
            var quantityDialogs = FindAll<CraftQuantityDialog>(scene);
            var recipeCraftPanels = FindAll<RecipeCraftPanelController>(scene);

            ValidateExpectedCount(craftPanels, nameof(CraftPanelController), report);
            ValidateExpectedCount(freeCraftPanels, nameof(FreeCraftPanelController), report);
            ValidateExpectedCount(quantityDialogs, nameof(CraftQuantityDialog), report);
            ValidateExpectedCount(recipeCraftPanels, nameof(RecipeCraftPanelController), report);

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
                "_materialSlotsView",
                "_detailPanel",
                "_craftButton",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedPanel = new SerializedObject(panel);
            var materialSlotsView = GetReference<FreeCraftMaterialSlotsView>(
                serializedPanel,
                "_materialSlotsView"
            );
            var craftButton = GetReference<Button>(serializedPanel, "_craftButton");
            var detailPanel = GetReference<ItemDetailPanel>(serializedPanel, "_detailPanel");
            var inventory = GetReference<InventoryView>(serializedPanel, "_inventory");

            ValidateFreeCraftOwnedReference(panel, materialSlotsView, "_materialSlotsView", report);
            ValidateFreeCraftOwnedReference(panel, inventory, "_inventory", report);
            ValidateFreeCraftOwnedReference(panel, craftButton, "_craftButton", report);
            ValidateFreeCraftOwnedReference(panel, detailPanel, "_detailPanel", report);

            if (materialSlotsView != null)
                ValidateFreeCraftMaterialSlotsView(materialSlotsView, report);
        }

        private static void ValidateFreeCraftOwnedReference(
            FreeCraftPanelController panel,
            Component reference,
            string fieldName,
            UIValidationReport report
        )
        {
            if (reference == null)
                return;

            if (!reference.transform.IsChildOf(panel.transform))
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    fieldName,
                    $"{fieldName}は同じFreeCraft画面配下のComponentを設定してください。",
                    panel
                );
                return;
            }

            if (reference.GetComponentInParent<RecipeCraftPanelController>(true) != null)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    fieldName,
                    $"{fieldName}にRecipeCraft側のComponentを設定しないでください。",
                    panel
                );
            }
        }

        private static void ValidateFreeCraftMaterialSlotsView(
            FreeCraftMaterialSlotsView view,
            UIValidationReport report
        )
        {
            var slots = new SerializedObject(view).FindProperty("_slots");
            string path = UIHierarchyPathUtility.GetPath(view.transform);
            if (slots == null)
            {
                report.Error(path, "_slots", "MaterialSlot一覧が見つかりません。", view);
                return;
            }

            if (slots.arraySize != FreeCraftMaterialAssignmentState.RequiredSlotCount)
            {
                report.Error(
                    path,
                    "_slots",
                    $"FreeCraftのMaterialSlotを表示順に正確に{FreeCraftMaterialAssignmentState.RequiredSlotCount}つ設定してください。現在: {slots.arraySize}",
                    view
                );
            }

            var registeredSlots = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue;
                if (slot == null)
                {
                    report.Error(
                        path,
                        $"_slots[{i}]",
                        "MaterialSlot参照を設定してください。",
                        view
                    );
                    continue;
                }

                if (!registeredSlots.Add(slot))
                {
                    report.Error(
                        path,
                        $"_slots[{i}]",
                        "同じMaterialSlotが重複登録されています。",
                        view
                    );
                }
            }
        }

        private static void ValidateCraftPanel(
            CraftPanelController panel,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_recipeDB",
                "_loadingOverlayView",
                "_resultPanelView",
                "_warningToastView",
                "_closeButton",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedObject = new SerializedObject(panel);
            var resultView = GetReference<CraftResultPanelView>(
                serializedObject,
                "_resultPanelView"
            );
            var warningView = GetReference<CraftWarningToastView>(
                serializedObject,
                "_warningToastView"
            );
            var loadingView = GetReference<CraftLoadingOverlayView>(
                serializedObject,
                "_loadingOverlayView"
            );
            var duration = serializedObject.FindProperty("_craftFlowDurationSeconds");
            if (duration == null)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    "_craftFlowDurationSeconds",
                    "共通CraftFlow時間のSerializeFieldが見つかりません。",
                    panel
                );
            }
            else if (!Mathf.Approximately(duration.floatValue, 1f))
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    "_craftFlowDurationSeconds",
                    $"FreeCraft / RecipeCraft共通の調合演出時間を1秒に設定してください。現在: {duration.floatValue}秒",
                    panel
                );
            }
            else
            {
                report.Ok(
                    UIHierarchyPathUtility.GetPath(panel.transform),
                    "_craftFlowDurationSeconds",
                    "共通CraftFlow時間が1秒に設定されています。",
                    panel
                );
            }

            if (resultView != null)
                ValidateResultPanelView(resultView, report);
            if (warningView != null)
                ValidateWarningToastView(warningView, report);
            if (loadingView != null)
                ValidateLoadingOverlayView(loadingView, report);
        }

        private static void ValidateResultPanelView(
            CraftResultPanelView view,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_canvasGroup",
                "_closeOnSelfClick",
                "_background",
                "_title",
                "_itemImage",
                "_itemName",
            };
            ValidateRequiredReferences(view, requiredFields, report);

            var serializedView = new SerializedObject(view);
            var canvasGroup = GetReference<CanvasGroup>(serializedView, "_canvasGroup");
            var closeOnSelfClick = GetReference<CloseOnSelfClick>(
                serializedView,
                "_closeOnSelfClick"
            );
            var background = GetReference<Graphic>(serializedView, "_background");

            if (canvasGroup != null && canvasGroup.gameObject != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_canvasGroup",
                    "CraftResultPanelViewと同じGameObjectのCanvasGroupを設定してください。",
                    view
                );
            }

            if (closeOnSelfClick != null && closeOnSelfClick.gameObject != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_closeOnSelfClick",
                    "CraftResultPanelViewと同じGameObjectのCloseOnSelfClickを設定してください。",
                    view
                );
            }

            ValidateResultPanel(view.gameObject, report);
            if (background != null)
                ValidateRaycastGraphic(background.gameObject, "ResultPanel背景", report);
        }

        private static void ValidateWarningToastView(
            CraftWarningToastView view,
            UIValidationReport report
        )
        {
            string[] requiredFields = { "_text", "_canvasGroup", "_rectTransform" };
            ValidateRequiredReferences(view, requiredFields, report);

            var serializedView = new SerializedObject(view);
            var warningText = GetReference<TMP_Text>(serializedView, "_text");
            var warningCanvasGroup = GetReference<CanvasGroup>(serializedView, "_canvasGroup");
            var warningRect = GetReference<RectTransform>(serializedView, "_rectTransform");
            ValidateWarningMessage(serializedView, view, "_categoryMismatchMessage", report);
            ValidateWarningMessage(serializedView, view, "_equippedMaterialMessage", report);
            ValidateWarningMessage(serializedView, view, "_missingMaterialsMessage", report);

            if (warningText != null && warningText.gameObject != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_text",
                    "CraftWarningToastViewと同じGameObjectのTMP_Textを設定してください。",
                    view
                );
            }

            if (warningCanvasGroup != null && warningCanvasGroup.gameObject != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_canvasGroup",
                    "CraftWarningToastViewと同じGameObjectのCanvasGroupを設定してください。",
                    view
                );
            }

            if (warningRect != null && warningRect.gameObject != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_rectTransform",
                    "CraftWarningToastView自身のRectTransformを設定してください。",
                    view
                );
            }

            ValidateWarningText(warningText, report);
        }

        private static void ValidateWarningMessage(
            SerializedObject serializedView,
            CraftWarningToastView view,
            string fieldName,
            UIValidationReport report
        )
        {
            var property = serializedView.FindProperty(fieldName);
            string path = UIHierarchyPathUtility.GetPath(view.transform);
            if (property == null)
            {
                report.Error(
                    path,
                    fieldName,
                    "Warning文言のSerializeFieldが見つかりません。",
                    view
                );
            }
            else if (string.IsNullOrWhiteSpace(property.stringValue))
            {
                report.Warning(
                    path,
                    fieldName,
                    "Warning文言が空です。CraftWarningToastViewで文言を設定してください。",
                    view
                );
            }
            else
            {
                report.Ok(path, fieldName, "Warning文言が設定されています。", view);
            }
        }

        private static void ValidateLoadingOverlayView(
            CraftLoadingOverlayView view,
            UIValidationReport report
        )
        {
            string[] requiredFields = { "_root", "_gear" };
            ValidateRequiredReferences(view, requiredFields, report);

            var serializedView = new SerializedObject(view);
            var root = GetReference<GameObject>(serializedView, "_root");
            if (root != null && root != view.gameObject)
            {
                report.Error(
                    view.name,
                    "_root",
                    "CraftLoadingOverlayViewをLoadingPanel rootへ付け、_rootに同じGameObjectを設定してください。",
                    view
                );
            }
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
            RecipeCraftPanelController panel,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_recipeDB",
                "_craftPanel",
                "_recipeListView",
                "_categoryTabGroup",
                "_detailPanel",
                "_materialRowsView",
                "_quantityDialogController",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedPanel = new SerializedObject(panel);
            var materialRowsView = GetReference<RecipeCraftMaterialRowsView>(
                serializedPanel,
                "_materialRowsView"
            );
            if (materialRowsView != null)
            {
                if (!materialRowsView.transform.IsChildOf(panel.transform))
                {
                    report.Error(
                        UIHierarchyPathUtility.GetPath(panel.transform),
                        "_materialRowsView",
                        "同じRecipeCraft画面配下のRecipeCraftMaterialRowsViewを設定してください。",
                        panel
                    );
                }

                ValidateRecipeCraftMaterialRowsView(materialRowsView, report);
            }

            ValidateRecipeCategoryTabGroup(panel, serializedPanel, report);
            var recipeListView = GetReference<RecipeListView>(serializedPanel, "_recipeListView");
            if (recipeListView != null)
                ValidateRecipeListView(recipeListView, report);
        }

        private static void ValidateRecipeListView(RecipeListView view, UIValidationReport report)
        {
            string[] requiredFields = { "_content", "_slotPrefab" };
            ValidateRequiredReferences(view, requiredFields, report);

            var serializedView = new SerializedObject(view);
            var recipeSlotPrefab = GetReference<GameObject>(serializedView, "_slotPrefab");
            if (recipeSlotPrefab == null)
                return;

            string path = AssetDatabase.GetAssetPath(recipeSlotPrefab);
            if (path != RecipeSlotPath || recipeSlotPrefab.GetComponent<RecipeSlot>() == null)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(view.transform),
                    "_slotPrefab",
                    $"'{RecipeSlotPath}' のRecipeSlot Variantを設定してください。現在: '{path}'",
                    view
                );
            }
            else
            {
                report.Ok(
                    UIHierarchyPathUtility.GetPath(view.transform),
                    "_slotPrefab",
                    "正しいRecipeSlot Variantを参照しています。",
                    view
                );
            }
        }

        private static void ValidateRecipeCraftMaterialRowsView(
            RecipeCraftMaterialRowsView view,
            UIValidationReport report
        )
        {
            var materialRows = new SerializedObject(view).FindProperty("_rows");
            if (materialRows == null || materialRows.arraySize != 2)
            {
                report.Error(
                    UIHierarchyPathUtility.GetPath(view.transform),
                    "_rows",
                    $"RecipeCraftMaterialRowsViewには固定RecipeMaterialRowを正確に2件設定してください。現在: {materialRows?.arraySize ?? 0}",
                    view
                );
            }

            if (materialRows != null)
            {
                var registeredRows = new HashSet<UnityEngine.Object>();
                for (int i = 0; i < materialRows.arraySize; i++)
                {
                    var row = materialRows.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (row == null)
                    {
                        report.Error(
                            UIHierarchyPathUtility.GetPath(view.transform),
                            $"_rows[{i}]",
                            "RecipeMaterialRow参照を設定してください。",
                            view
                        );
                        continue;
                    }

                    if (!registeredRows.Add(row))
                    {
                        report.Error(
                            UIHierarchyPathUtility.GetPath(view.transform),
                            $"_rows[{i}]",
                            "同じRecipeMaterialRowを重複登録しないでください。",
                            view
                        );
                    }

                    if (
                        row is Component rowComponent
                        && !rowComponent.transform.IsChildOf(view.transform)
                    )
                    {
                        report.Error(
                            UIHierarchyPathUtility.GetPath(view.transform),
                            $"_rows[{i}]",
                            "RecipeCraftMaterialRowsView配下のRecipeMaterialRowを設定してください。",
                            view
                        );
                    }
                }
            }
        }

        private static void ValidateRecipeCategoryTabGroup(
            RecipeCraftPanelController panel,
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
