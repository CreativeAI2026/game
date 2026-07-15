using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.UI.Common;
using CreativeAI.UI.CraftingUI;
using CreativeAI.UI.InventoryUI;
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
            SlotPrefabValidator.ValidateFromMenu();
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
            var quantityDialogs = FindAll<CraftQuantityDialog>(scene);
            var recipeCraftPanels = FindAll<RecipeCraftPanel>(scene);
            var itemUseDialogs = FindAll<ItemUseDialogPanel>(scene);
            var inventoryPanelControllers = FindAll<InventoryPanelController>(scene);
            var inventories = FindAll<Inventory>(scene);

            ValidateExpectedCount(craftPanels, nameof(CraftPanel), report);
            ValidateExpectedCount(quantityDialogs, nameof(CraftQuantityDialog), report);
            ValidateExpectedCount(recipeCraftPanels, nameof(RecipeCraftPanel), report);
            ValidateExpectedCount(
                itemUseDialogs,
                nameof(ItemUseDialogPanel),
                report,
                allowMultiple: true
            );

            foreach (var craftPanel in craftPanels)
                ValidateCraftPanel(craftPanel, report);
            foreach (var quantityDialog in quantityDialogs)
                ValidateQuantityDialog(quantityDialog, report);
            foreach (var recipeCraftPanel in recipeCraftPanels)
                ValidateRecipeCraftPanel(recipeCraftPanel, report);
            foreach (var itemUseDialog in itemUseDialogs)
                ValidateItemUseDialog(itemUseDialog, report);
            foreach (var inventoryPanelController in inventoryPanelControllers)
                ValidateInventoryPanelController(inventoryPanelController, report);
            foreach (var inventory in inventories)
                ValidateInventory(inventory, report);
        }

        private static void ValidateInventoryPanelController(
            InventoryPanelController panel,
            UIValidationReport report
        )
        {
            string[] requiredFields = { "_inventory", "_itemUseDialogPanel" };
            ValidateRequiredReferences(panel, requiredFields, report);
        }

        private static void ValidateInventory(Inventory inventory, UIValidationReport report)
        {
            string[] requiredFields = { "_tabGroup", "_detailPanel", "_slotsRoot", "_slotPrefab" };
            ValidateRequiredReferences(inventory, requiredFields, report);
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
            var warningText = GetReference<Component>(serializedObject, "_warningText");
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

            var resultPanel = GetReference<GameObject>(serializedObject, "_resultPanel");
            ValidateResultPanel(resultPanel, report);
        }

        private static void ValidateResultPanel(GameObject resultPanel, UIValidationReport report)
        {
            if (resultPanel == null)
                return;

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
            if (target != resultPanel)
            {
                report.Error(
                    resultPanel.name,
                    "Target To Hide",
                    "ResultPanel自身を設定してください。",
                    catchers[0]
                );
            }
            else
            {
                report.Ok(
                    resultPanel.name,
                    "Target To Hide",
                    "ResultPanel自身を閉じる構成です。",
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
                "_recipeTabGroup",
                "_detailPanel",
                "_materialList",
                "_quantityDialogPanel",
                "_quantityDialog",
                "_quantityDialogController",
            };
            ValidateRequiredReferences(panel, requiredFields, report);

            var serializedPanel = new SerializedObject(panel);
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

        private static void ValidateItemUseDialog(
            ItemUseDialogPanel dialog,
            UIValidationReport report
        )
        {
            string[] requiredFields =
            {
                "_closeOnSelfClick",
                "_backgroundImage",
                "_dialogRoot",
                "_itemIconImage",
                "_itemNameText",
                "_itemEffectText",
                "_useButton",
            };
            ValidateRequiredReferences(dialog, requiredFields, report);

            var serializedDialog = new SerializedObject(dialog);
            var background = GetReference<Image>(serializedDialog, "_backgroundImage");
            var dialogRoot = GetReference<RectTransform>(serializedDialog, "_dialogRoot");
            if (background != null && !background.raycastTarget)
            {
                report.Error(
                    dialog.name,
                    "_backgroundImage",
                    "Raycast TargetをONにしてください。",
                    dialog
                );
            }
            if (dialogRoot != null)
                ValidateRaycastGraphic(dialogRoot.gameObject, "DialogRoot", report);
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
                        owner.name,
                        fieldName,
                        "SerializeFieldが見つかりません。Validatorの定義を更新してください。",
                        owner
                    );
                }
                else if (property.objectReferenceValue == null)
                {
                    report.Error(
                        owner.name,
                        fieldName,
                        "Inspectorで必須参照を設定してください。",
                        owner
                    );
                }
                else
                {
                    report.Ok(
                        owner.name,
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
