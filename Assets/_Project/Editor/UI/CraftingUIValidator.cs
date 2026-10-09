using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class CraftingUIValidator
    {
        private const string FieldArea01Path =
            "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";

        [MenuItem("Tools/CreativeAI/UI/Validate Crafting UI")]
        public static void ValidateFromMenu()
        {
            var report = new UIValidationReport("Crafting UI");
            GameObject root = null;
            Scene scene = default;

            try
            {
                root = PrefabUtility.LoadPrefabContents(FieldArea01Path);
                scene = root.scene;
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
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
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

            ValidateExpectedCount(craftPanels, nameof(CraftPanelController), report);

            foreach (var craftPanel in craftPanels)
                ValidateCraftPanel(craftPanel, report);
        }

        private static void ValidateMaterialReferences(
            CraftPanelController panel,
            UIValidationReport report
        )
        {
            var serializedPanel = new SerializedObject(panel);
            var craftButton = GetReference<Button>(serializedPanel, "_craftButton");
            var craftedItemSlot = GetReference<SlotIconView>(serializedPanel, "_craftedItemSlot");
            var inventory = GetReference<InventoryView>(serializedPanel, "_inventory");

            ValidateOwnedReference(panel, inventory, "_inventory", report);
            ValidateOwnedReference(panel, craftButton, "_craftButton", report);
            ValidateOwnedReference(panel, craftedItemSlot, "_craftedItemSlot", report);
            ValidateMaterialSlots(panel, serializedPanel, report);
        }

        private static void ValidateOwnedReference(
            CraftPanelController panel,
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
                    $"{fieldName}は同じ調合画面配下のComponentを設定してください。",
                    panel
                );
            }
        }

        private static void ValidateMaterialSlots(
            CraftPanelController panel,
            SerializedObject serializedPanel,
            UIValidationReport report
        )
        {
            var slots = serializedPanel.FindProperty("_materialSlots");
            string path = UIHierarchyPathUtility.GetPath(panel.transform);
            if (slots == null || slots.arraySize != CraftPanelController.MaterialSlotCount)
            {
                report.Error(
                    path,
                    "_materialSlots",
                    $"MaterialSlotを表示順に正確に{CraftPanelController.MaterialSlotCount}つ設定してください。現在: {slots?.arraySize ?? 0}",
                    panel
                );
                return;
            }

            var registeredSlots = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as MaterialSlot;
                if (slot == null)
                {
                    report.Error(
                        path,
                        $"_materialSlots[{i}]",
                        "MaterialSlot参照を設定してください。",
                        panel
                    );
                    continue;
                }

                if (!registeredSlots.Add(slot))
                    report.Error(
                        path,
                        $"_materialSlots[{i}]",
                        "同じMaterialSlotが重複登録されています。",
                        panel
                    );

                ValidateOwnedReference(panel, slot, $"_materialSlots[{i}]", report);
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
                "_closeButton",
                "_inventory",
                "_craftedItemSlot",
                "_craftButton",
                "_loadingRoot",
                "_loadingGear",
                "_resultCanvasGroup",
                "_resultCloseOnClick",
                "_resultItemImage",
                "_resultItemName",
                "_resultItemParameters",
                "_warningText",
                "_warningCanvasGroup",
            };
            ValidateRequiredReferences(panel, requiredFields, report);
            ValidateMaterialReferences(panel, report);

            var serializedObject = new SerializedObject(panel);
            string path = UIHierarchyPathUtility.GetPath(panel.transform);
            var duration = serializedObject.FindProperty("_craftFlowDurationSeconds");
            if (duration == null)
            {
                report.Error(
                    path,
                    "_craftFlowDurationSeconds",
                    "CraftFlow時間のSerializeFieldが見つかりません。",
                    panel
                );
            }
            else if (!Mathf.Approximately(duration.floatValue, 1f))
            {
                report.Error(
                    path,
                    "_craftFlowDurationSeconds",
                    $"調合演出時間を1秒に設定してください。現在: {duration.floatValue}秒",
                    panel
                );
            }
            else
            {
                report.Ok(
                    path,
                    "_craftFlowDurationSeconds",
                    "共通CraftFlow時間が1秒に設定されています。",
                    panel
                );
            }

            var loadingRoot = GetReference<GameObject>(serializedObject, "_loadingRoot");
            var loadingGear = GetReference<RectTransform>(serializedObject, "_loadingGear");
            if (
                loadingRoot != null
                && loadingGear != null
                && !loadingGear.IsChildOf(loadingRoot.transform)
            )
                report.Error(
                    path,
                    "_loadingGear",
                    "LoadingPanel配下の歯車を設定してください。",
                    panel
                );

            var resultCanvasGroup = GetReference<CanvasGroup>(
                serializedObject,
                "_resultCanvasGroup"
            );
            var resultCloseOnClick = GetReference<CloseOnSelfClick>(
                serializedObject,
                "_resultCloseOnClick"
            );
            if (resultCanvasGroup != null)
            {
                if (
                    resultCloseOnClick != null
                    && resultCloseOnClick.gameObject != resultCanvasGroup.gameObject
                )
                    report.Error(
                        path,
                        "_resultCloseOnClick",
                        "ResultPanel RootのCloseOnSelfClickを設定してください。",
                        panel
                    );
                ValidateResultPanel(resultCanvasGroup.gameObject, report);
            }

            var warningText = GetReference<TMP_Text>(serializedObject, "_warningText");
            var warningCanvasGroup = GetReference<CanvasGroup>(
                serializedObject,
                "_warningCanvasGroup"
            );
            if (
                warningText != null
                && warningCanvasGroup != null
                && warningCanvasGroup.gameObject != warningText.gameObject
            )
                report.Error(
                    path,
                    "_warningCanvasGroup",
                    "WarningTextと同じGameObjectのCanvasGroupを設定してください。",
                    panel
                );
            ValidateWarningText(warningText, report);
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
                    "ResultPanelはCraftPanelController.HideResult()経由で閉じるため、CloseOnSelfClick.TargetToHideは使用しないでください。",
                    catchers[0]
                );
            }
            else
            {
                report.Ok(
                    resultPanel.name,
                    "Target To Hide",
                    "Noneです。Runtime actionからHideResult()を使用します。",
                    catchers[0]
                );
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
