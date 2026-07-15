using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.UI;
using CreativeAI.UI.Common;
using CreativeAI.UI.InventoryUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    public static class InventoryUIValidator
    {
        private const string FieldArea01Path = "Assets/_Project/Scenes/Field/Field_Area01.unity";
        private const string ItemSlotPath =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/ItemSlot.prefab";

        [MenuItem("Tools/CreativeAI/UI/Validate Inventory UI")]
        public static void ValidateFromMenu()
        {
            var report = new UIValidationReport("Inventory UI");
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

        private static void ValidateScene(Scene scene, UIValidationReport report)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Error(FieldArea01Path, "Scene", "Sceneを読み込めません。", null);
                return;
            }

            var panelControllers = FindAll<InventoryPanelController>(scene);
            var inventories = FindAll<Inventory>(scene);
            var itemUseDialogs = FindAll<ItemUseDialogPanel>(scene);

            ValidatePresence(panelControllers, nameof(InventoryPanelController), report);
            ValidatePresence(inventories, nameof(Inventory), report);
            ValidatePresence(itemUseDialogs, nameof(ItemUseDialogPanel), report);

            foreach (var panelController in panelControllers)
                ValidateInventoryPanelController(panelController, report);
            foreach (var inventory in inventories)
                ValidateInventory(inventory, report);
            foreach (var itemUseDialog in itemUseDialogs)
                ValidateItemUseDialog(itemUseDialog, report);
        }

        private static void ValidateInventoryPanelController(
            InventoryPanelController panel,
            UIValidationReport report
        )
        {
            string[] requiredFields = { "_inventory", "_itemUseDialogPanel" };
            ValidateRequiredReferences(panel, requiredFields, report);
            ValidateNoMissingScripts(panel.gameObject, report);
        }

        private static void ValidateInventory(Inventory inventory, UIValidationReport report)
        {
            string[] requiredFields = { "_tabGroup", "_detailPanel", "_slotsRoot", "_slotPrefab" };
            ValidateRequiredReferences(inventory, requiredFields, report);

            var serializedInventory = new SerializedObject(inventory);
            var slotsRoot = GetReference<Transform>(serializedInventory, "_slotsRoot");
            var slotPrefab = GetReference<ItemSlot>(serializedInventory, "_slotPrefab");

            ValidateSlotsRoot(inventory, slotsRoot, report);
            ValidateItemSlotPrefab(inventory, slotPrefab, report);
        }

        private static void ValidateSlotsRoot(
            Inventory inventory,
            Transform slotsRoot,
            UIValidationReport report
        )
        {
            if (slotsRoot == null)
                return;

            if (slotsRoot is not RectTransform slotsRect)
            {
                report.Error(
                    inventory.name,
                    "_slotsRoot",
                    "ScrollRect.contentに使用できるRectTransformを設定してください。",
                    inventory
                );
                return;
            }

            var scrollRect = slotsRoot.GetComponentInParent<ScrollRect>(true);
            if (scrollRect == null)
            {
                report.Error(
                    inventory.name,
                    "_slotsRoot",
                    "親階層にScrollRectがありません。対応するScrollRect.contentを確認してください。",
                    inventory
                );
            }
            else if (scrollRect.content != slotsRect)
            {
                report.Error(
                    inventory.name,
                    "_slotsRoot",
                    $"'{scrollRect.name}' のScrollRect.contentと一致していません。contentに'{slotsRoot.name}'を設定してください。",
                    inventory
                );
            }
            else
            {
                report.Ok(
                    inventory.name,
                    "_slotsRoot",
                    $"'{scrollRect.name}' のScrollRect.contentと一致しています。",
                    inventory
                );
            }

            bool hasUnexpectedChild = false;
            for (int i = 0; i < slotsRoot.childCount; i++)
            {
                var child = slotsRoot.GetChild(i);
                if (child.GetComponent<ItemSlot>() != null)
                    continue;

                hasUnexpectedChild = true;
                report.Error(
                    child.name,
                    "_slotsRoot child",
                    "ItemSlotではない固定UIが_slotsRoot直下にあります。固定UIをスロット生成領域の外へ移してください。",
                    child
                );
            }

            if (!hasUnexpectedChild)
            {
                report.Ok(
                    inventory.name,
                    "_slotsRoot children",
                    "直下にItemSlot以外の固定UIはありません。",
                    inventory
                );
            }
        }

        private static void ValidateItemSlotPrefab(
            Inventory inventory,
            ItemSlot slotPrefab,
            UIValidationReport report
        )
        {
            if (slotPrefab == null)
                return;

            var root = slotPrefab.gameObject;
            string path = AssetDatabase.GetAssetPath(root);
            if (path != ItemSlotPath)
            {
                report.Error(
                    inventory.name,
                    "_slotPrefab",
                    $"正しいItemSlot Variantを設定してください。期待値: '{ItemSlotPath}'、現在: '{path}'",
                    inventory
                );
            }
            else if (PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Variant)
            {
                report.Error(
                    inventory.name,
                    "_slotPrefab",
                    "設定されたItemSlot PrefabはPrefab Variantではありません。",
                    inventory
                );
            }
            else
            {
                report.Ok(
                    inventory.name,
                    "_slotPrefab",
                    "正しいItemSlot Variantを参照しています。",
                    inventory
                );
            }

            ValidatePrefabComponent<ItemSlot>(root, inventory, report);
            ValidatePrefabComponent<SlotIconView>(root, inventory, report);
            ValidatePrefabComponent<SlotCountBadgeView>(root, inventory, report);
            ValidatePrefabComponent<SlotMarkerView>(root, inventory, report);
        }

        private static void ValidatePrefabComponent<T>(
            GameObject prefabRoot,
            Inventory inventory,
            UIValidationReport report
        )
            where T : Component
        {
            var component = prefabRoot.GetComponentInChildren<T>(true);
            if (component == null)
            {
                report.Error(
                    inventory.name,
                    "_slotPrefab",
                    $"ItemSlot Variantに{typeof(T).Name}がありません。Prefab上で設定してください。",
                    inventory
                );
            }
            else
            {
                report.Ok(
                    prefabRoot.name,
                    typeof(T).Name,
                    $"{typeof(T).Name}が設定されています。",
                    component
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
            var catcher = GetReference<CloseOnSelfClick>(serializedDialog, "_closeOnSelfClick");

            ValidateRaycastGraphic(background, "_backgroundImage", report);
            if (dialogRoot != null)
                ValidateRaycastGraphic(
                    dialogRoot.GetComponent<Graphic>(),
                    "_dialogRoot",
                    report,
                    dialogRoot
                );
            if (catcher != null)
                ValidateItemUseCloseAction(dialog, catcher, report);
        }

        private static void ValidateRaycastGraphic(
            Graphic graphic,
            string fieldName,
            UIValidationReport report,
            UnityEngine.Object context = null
        )
        {
            if (graphic == null)
            {
                report.Error(
                    context != null ? context.name : "Missing Graphic",
                    fieldName,
                    "Graphicがありません。ImageなどのGraphicを設定してください。",
                    context
                );
            }
            else if (!graphic.raycastTarget)
            {
                report.Error(
                    graphic.name,
                    fieldName,
                    "Raycast TargetをONにしてください。",
                    graphic
                );
            }
            else
            {
                report.Ok(graphic.name, fieldName, "Raycast TargetがONです。", graphic);
            }
        }

        private static void ValidateItemUseCloseAction(
            ItemUseDialogPanel dialog,
            CloseOnSelfClick catcher,
            UIValidationReport report
        )
        {
            var serializedCatcher = new SerializedObject(catcher);
            var targetToHide = GetReference<GameObject>(serializedCatcher, "_targetToHide");
            if (targetToHide != null)
            {
                report.Error(
                    catcher.name,
                    "Target To Hide",
                    "状態を安全にクリアするためNoneにし、ItemUseDialogPanel.Hide()経由で閉じてください。",
                    catcher
                );
            }
            else
            {
                report.Ok(
                    catcher.name,
                    "Target To Hide",
                    "Noneです。直接SetActive(false)する構成ではありません。",
                    catcher
                );
            }

            var unityEvent = serializedCatcher.FindProperty("_onSelfClick");
            var persistentCalls = unityEvent?.FindPropertyRelative("m_PersistentCalls");
            var calls = persistentCalls?.FindPropertyRelative("m_Calls");
            if (calls == null || calls.arraySize != 1)
            {
                report.Error(
                    catcher.name,
                    "On Self Click",
                    $"ItemUseDialogPanel.Hide()を1件だけ登録してください。現在: {calls?.arraySize ?? 0}件",
                    catcher
                );
                return;
            }

            var call = calls.GetArrayElementAtIndex(0);
            var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
            string methodName = call.FindPropertyRelative("m_MethodName")?.stringValue;
            if (target != dialog || methodName != nameof(ItemUseDialogPanel.Hide))
            {
                report.Error(
                    catcher.name,
                    "On Self Click",
                    "ItemUseDialogPanel.Hide()を登録してください。Target To Hideや別メソッドは使用しないでください。",
                    catcher
                );
            }
            else
            {
                report.Ok(
                    catcher.name,
                    "On Self Click",
                    "ItemUseDialogPanel.Hide()が1件だけ登録されています。",
                    catcher
                );
            }
        }

        private static void ValidateNoMissingScripts(GameObject root, UIValidationReport report)
        {
            int missingCount = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                int childMissingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    child.gameObject
                );
                if (childMissingCount <= 0)
                    continue;

                missingCount += childMissingCount;
                report.Error(
                    child.name,
                    "Missing Script",
                    $"Missing Scriptが{childMissingCount}件あります。不要なComponentを削除するか正しいScriptを設定してください。",
                    child
                );
            }

            if (missingCount == 0)
                report.Ok(root.name, "Missing Script", "Missing Scriptはありません。", root);
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
                        $"{property.objectReferenceValue.name}を参照しています。",
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

        private static T[] FindAll<T>(Scene scene)
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void ValidatePresence<T>(
            IReadOnlyCollection<T> components,
            string componentName,
            UIValidationReport report
        )
            where T : Component
        {
            if (components.Count == 0)
            {
                report.Error(FieldArea01Path, componentName, "Scene内に1件以上必要です。", null);
            }
            else
            {
                report.Ok(
                    FieldArea01Path,
                    componentName,
                    $"Scene内に{components.Count}件あります。",
                    components.FirstOrDefault()
                );
            }
        }
    }
}
