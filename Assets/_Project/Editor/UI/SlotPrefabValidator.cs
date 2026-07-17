using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.UI;
using CreativeAI.UI.CraftingUI;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    public static class SlotPrefabValidator
    {
        private const string ItemLikeSlotBasePath =
            "Assets/_Project/Features/UI/Common/Prefabs/ItemLikeSlotBase.prefab";
        private const string HolderSlotBasePath =
            "Assets/_Project/Features/UI/Common/Prefabs/HolderSlotBase.prefab";
        private const string ItemSlotPath =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/ItemSlot.prefab";
        private const string RecipeSlotPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/RecipeSlot.prefab";
        private const string MaterialSlotPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/MaterialSlot.prefab";
        private const string EquipmentSlotPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/EquipmentSlot.prefab";

        private static readonly Type[] DerivedSlotTypes =
        {
            typeof(ItemSlot),
            typeof(RecipeSlot),
            typeof(MaterialSlot),
            typeof(EquipmentSlot),
        };

        private static readonly HashSet<string> NonRaycastDecorationNames = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "Icon",
            "Frame",
            "SelectedFrame",
            "CountBadge",
            "CountText",
            "EquippedMarker",
            "CraftAssignedMarker",
            "EquippedDimOverlay",
            "EmptyText",
            "SlotLabel",
        };

        [MenuItem("Tools/CreativeAI/UI/Validate Slot Prefabs")]
        public static void ValidateFromMenu()
        {
            var report = new UIValidationReport("Slot Prefabs");

            ValidateBasePrefab(
                ItemLikeSlotBasePath,
                report,
                typeof(SlotIconView),
                typeof(SlotHoverView),
                typeof(SlotSelectionView)
            );
            ValidateBasePrefab(
                HolderSlotBasePath,
                report,
                typeof(SlotIconView),
                typeof(SlotEmptyView),
                typeof(SlotHoverView),
                typeof(SlotFrameView)
            );

            ValidateVariant(
                ItemSlotPath,
                ItemLikeSlotBasePath,
                report,
                new[]
                {
                    typeof(ItemSlot),
                    typeof(SlotIconView),
                    typeof(SlotCountBadgeView),
                    typeof(SlotHoverView),
                    typeof(SlotSelectionView),
                    typeof(SlotMarkerView),
                },
                new[] { typeof(SlotFrameView), typeof(SlotEmptyView) }
            );
            ValidateVariant(
                RecipeSlotPath,
                ItemLikeSlotBasePath,
                report,
                new[]
                {
                    typeof(RecipeSlot),
                    typeof(SlotIconView),
                    typeof(SlotHoverView),
                    typeof(SlotSelectionView),
                },
                new[]
                {
                    typeof(SlotFrameView),
                    typeof(SlotEmptyView),
                    typeof(SlotCountBadgeView),
                    typeof(SlotMarkerView),
                }
            );
            ValidateVariant(
                MaterialSlotPath,
                HolderSlotBasePath,
                report,
                new[]
                {
                    typeof(MaterialSlot),
                    typeof(SlotIconView),
                    typeof(SlotEmptyView),
                    typeof(SlotHoverView),
                    typeof(SlotFrameView),
                },
                new[]
                {
                    typeof(SlotSelectionView),
                    typeof(SlotCountBadgeView),
                    typeof(SlotMarkerView),
                }
            );
            ValidateVariant(
                EquipmentSlotPath,
                HolderSlotBasePath,
                report,
                new[]
                {
                    typeof(EquipmentSlot),
                    typeof(SlotIconView),
                    typeof(SlotCountBadgeView),
                    typeof(SlotEmptyView),
                    typeof(SlotHoverView),
                    typeof(SlotFrameView),
                },
                new[] { typeof(SlotSelectionView), typeof(SlotMarkerView) }
            );

            report.Complete();
        }

        private static void ValidateBasePrefab(
            string path,
            UIValidationReport report,
            params Type[] expectedTypes
        )
        {
            var root = LoadPrefab(path, report);
            if (root == null)
                return;

            ValidateNoDuplicateComponents(root, report);
            foreach (var derivedType in DerivedSlotTypes)
                ValidateForbiddenComponent(
                    root,
                    derivedType,
                    report,
                    "Base Prefabに派生専用Componentを置かないでください。"
                );

            foreach (var expectedType in expectedTypes)
                ValidateSingleComponent(root, expectedType, report);

            if (path == ItemLikeSlotBasePath)
            {
                ValidateForbiddenComponent(
                    root,
                    typeof(SlotCountBadgeView),
                    report,
                    "ItemLikeSlotBaseにItemSlot専用のCountBadgeを置かないでください。"
                );
                ValidateForbiddenComponent(
                    root,
                    typeof(SlotMarkerView),
                    report,
                    "ItemLikeSlotBaseにItemSlot専用のMarkerを置かないでください。"
                );
            }
            else if (path == HolderSlotBasePath)
            {
                ValidateForbiddenComponent(
                    root,
                    typeof(SlotCountBadgeView),
                    report,
                    "HolderSlotBaseにVariant専用のCountBadgeを置かないでください。"
                );
                ValidateForbiddenComponent(
                    root,
                    typeof(SlotMarkerView),
                    report,
                    "HolderSlotBaseにItemSlot専用のMarkerを置かないでください。"
                );
                ValidateForbiddenComponent(
                    root,
                    typeof(SlotSelectionView),
                    report,
                    "HolderSlotBaseにItemLikeSlot用のSelectionを置かないでください。"
                );
            }

            ValidateViewReferences(root, report);
            ValidateDecorationRaycasts(root, report);
            ValidateCountBadgeLayout(root, report);
            report.Ok(root.name, "Base Prefab構成", "派生専用Componentの混入はありません。", root);
        }

        private static void ValidateVariant(
            string path,
            string expectedBasePath,
            UIValidationReport report,
            IReadOnlyCollection<Type> expectedTypes,
            IReadOnlyCollection<Type> forbiddenTypes
        )
        {
            var root = LoadPrefab(path, report);
            if (root == null)
                return;

            ValidateVariantSource(root, expectedBasePath, report);
            ValidateNoDuplicateComponents(root, report);

            foreach (var expectedType in expectedTypes)
                ValidateSingleComponent(root, expectedType, report);
            foreach (var forbiddenType in forbiddenTypes)
                ValidateForbiddenComponent(
                    root,
                    forbiddenType,
                    report,
                    "このVariantの想定構成には含まれません。"
                );

            ValidateViewReferences(root, report);
            ValidateSlotControllerReferences(root, report);
            ValidateDecorationRaycasts(root, report);
            ValidateCountBadgeLayout(root, report);
        }

        private static void ValidateDecorationRaycasts(GameObject root, UIValidationReport report)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!NonRaycastDecorationNames.Contains(graphic.gameObject.name))
                    continue;

                string hierarchyPath = GetPrefabHierarchyPath(root, graphic.transform);
                if (graphic.raycastTarget)
                {
                    report.Error(
                        $"{root.name} / {hierarchyPath}",
                        $"{graphic.GetType().Name}.raycastTarget",
                        $"装飾Graphic '{graphic.gameObject.name}' のRaycast TargetをOFFにしてください。",
                        graphic
                    );
                }
                else
                {
                    report.Ok(
                        $"{root.name} / {hierarchyPath}",
                        $"{graphic.GetType().Name}.raycastTarget",
                        "Raycast TargetはOFFです。",
                        graphic
                    );
                }
            }
        }

        private static void ValidateCountBadgeLayout(GameObject root, UIValidationReport report)
        {
            foreach (var badgeView in root.GetComponentsInChildren<SlotCountBadgeView>(true))
            {
                var serializedView = new SerializedObject(badgeView);
                var container =
                    serializedView.FindProperty("_container")?.objectReferenceValue
                    as RectTransform;
                var countText =
                    serializedView.FindProperty("_countText")?.objectReferenceValue as TMP_Text;

                if (container == null || countText == null)
                    continue;

                string badgePath = GetPrefabHierarchyPath(root, container);
                if (container.GetComponent<ContentSizeFitter>() != null)
                {
                    report.Error(
                        $"{root.name} / {badgePath}",
                        nameof(ContentSizeFitter),
                        "CountBadgeはSlotCountBadgeViewがサイズ制御するためContentSizeFitterを外してください。",
                        container
                    );
                }

                RectTransform countRect = countText.rectTransform;
                string countPath = GetPrefabHierarchyPath(root, countRect);
                if (!countRect.IsChildOf(container))
                {
                    report.Error(
                        $"{root.name} / {countPath}",
                        "_countText",
                        "CountTextをCountBadge配下に配置してください。",
                        countText
                    );
                }

                bool stretches =
                    Approximately(countRect.anchorMin, Vector2.zero)
                    && Approximately(countRect.anchorMax, Vector2.one);
                if (!stretches)
                {
                    report.Error(
                        $"{root.name} / {countPath}",
                        nameof(RectTransform),
                        "CountTextのAnchor Minを(0,0)、Anchor Maxを(1,1)にしてStretchさせてください。",
                        countRect
                    );
                }

                if (
                    countText.horizontalAlignment != HorizontalAlignmentOptions.Center
                    || countText.verticalAlignment != VerticalAlignmentOptions.Geometry
                )
                {
                    report.Error(
                        $"{root.name} / {countPath}",
                        nameof(TMP_Text.alignment),
                        "CountText alignment must be horizontal Center and vertical Midline.",
                        countText
                    );
                }

                if (countText.textWrappingMode != TextWrappingModes.NoWrap)
                {
                    report.Error(
                        $"{root.name} / {countPath}",
                        nameof(TMP_Text.textWrappingMode),
                        "CountTextのWrappingをOFFにしてください。",
                        countText
                    );
                }
            }
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x) && Mathf.Approximately(left.y, right.y);
        }

        private static string GetPrefabHierarchyPath(GameObject root, Transform target)
        {
            if (target == null)
                return "<null>";

            var names = new Stack<string>();
            for (
                Transform current = target;
                current != null;
                current = current == root.transform ? null : current.parent
            )
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static GameObject LoadPrefab(string path, UIValidationReport report)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
                report.Error(path, "Prefab Asset", "指定パスにPrefabがありません。", null);
            else
                report.Ok(root.name, "Prefab Asset", path, root);

            return root;
        }

        private static void ValidateVariantSource(
            GameObject root,
            string expectedBasePath,
            UIValidationReport report
        )
        {
            if (PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Variant)
            {
                report.Error(root.name, "Prefab Variant", "Prefab Variantではありません。", root);
                return;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            if (sourcePath != expectedBasePath)
            {
                report.Error(
                    root.name,
                    "Base Prefab",
                    $"継承元を '{expectedBasePath}' にしてください。現在: '{sourcePath}'",
                    root
                );
                return;
            }

            report.Ok(root.name, "Base Prefab", sourcePath, root);
        }

        private static void ValidateNoDuplicateComponents(
            GameObject root,
            UIValidationReport report
        )
        {
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                var components = target.GetComponents<Component>();
                if (components.Any(component => component == null))
                {
                    report.Error(
                        target.name,
                        "Missing Script",
                        "Missing Scriptを削除し、必要なComponentを明示的に設定してください。",
                        target.gameObject
                    );
                }

                foreach (
                    var duplicate in components
                        .Where(component => component != null)
                        .GroupBy(component => component.GetType())
                        .Where(group => group.Count() > 1)
                )
                {
                    report.Error(
                        target.name,
                        duplicate.Key.Name,
                        $"同一GameObjectに{duplicate.Count()}個あります。1個に整理してください。",
                        target.gameObject
                    );
                }
            }
        }

        private static void ValidateSingleComponent(
            GameObject root,
            Type componentType,
            UIValidationReport report
        )
        {
            var matches = root.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && component.GetType() == componentType)
                .ToArray();

            if (matches.Length != 1)
            {
                report.Error(
                    root.name,
                    componentType.Name,
                    $"Prefab階層内に1個必要です。現在: {matches.Length}個",
                    root
                );
                return;
            }

            if (matches[0].gameObject != root)
            {
                report.Error(
                    root.name,
                    componentType.Name,
                    "Slot Rootに配置してください。",
                    matches[0]
                );
                return;
            }

            report.Ok(root.name, componentType.Name, "Slot Rootに1個あります。", matches[0]);
        }

        private static void ValidateForbiddenComponent(
            GameObject root,
            Type componentType,
            UIValidationReport report,
            string fixMessage
        )
        {
            var matches = root.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && component.GetType() == componentType)
                .ToArray();
            if (matches.Length == 0)
                return;

            report.Error(root.name, componentType.Name, fixMessage, matches[0]);
        }

        private static void ValidateViewReferences(GameObject root, UIValidationReport report)
        {
            foreach (var view in root.GetComponentsInChildren<SlotIconView>(true))
                ValidateLocalReferences(view, root.transform, report, "_image");
            foreach (var view in root.GetComponentsInChildren<SlotEmptyView>(true))
                ValidateLocalReferences(view, root.transform, report, "_emptyObject");
            foreach (var view in root.GetComponentsInChildren<SlotHoverView>(true))
                ValidateLocalReferences(view, root.transform, report, "_hoverScale", "_visualRoot");
            foreach (var view in root.GetComponentsInChildren<SlotFrameView>(true))
                ValidateLocalReferences(view, root.transform, report, "_frame");
            foreach (var view in root.GetComponentsInChildren<SlotSelectionView>(true))
                ValidateLocalReferences(view, root.transform, report, "_selectedFrame");
            foreach (var view in root.GetComponentsInChildren<SlotCountBadgeView>(true))
            {
                ValidateLocalReferences(
                    view,
                    root.transform,
                    report,
                    "_container",
                    "_countText",
                    "_containerCanvasGroup",
                    "_countTextCanvasGroup",
                    "_backgroundImage"
                );
            }

            foreach (var view in root.GetComponentsInChildren<SlotMarkerView>(true))
            {
                ValidateLocalReferences(
                    view,
                    root.transform,
                    report,
                    "_equippedMarker",
                    "_craftAssignedMarker",
                    "_equippedDimOverlay"
                );
            }
        }

        private static void ValidateSlotControllerReferences(
            GameObject root,
            UIValidationReport report
        )
        {
            var itemSlot = root.GetComponent<ItemSlot>();
            if (itemSlot != null)
            {
                ValidateLocalReferences(
                    itemSlot,
                    root.transform,
                    report,
                    "_visualRootRect",
                    "_iconView",
                    "_countBadgeView",
                    "_hoverView",
                    "_selectionView",
                    "_markerView"
                );
            }

            var recipeSlot = root.GetComponent<RecipeSlot>();
            if (recipeSlot != null)
            {
                ValidateLocalReferences(
                    recipeSlot,
                    root.transform,
                    report,
                    "_visualRootRect",
                    "_iconView",
                    "_hoverView",
                    "_selectionView"
                );
            }

            var materialSlot = root.GetComponent<MaterialSlot>();
            if (materialSlot != null)
            {
                ValidateLocalReferences(
                    materialSlot,
                    root.transform,
                    report,
                    "_visualRootRect",
                    "_iconView",
                    "_emptyView",
                    "_hoverView",
                    "_frameView",
                    "_slotLabel"
                );
            }

            var equipmentSlot = root.GetComponent<EquipmentSlot>();
            if (equipmentSlot != null)
            {
                ValidateLocalReferences(
                    equipmentSlot,
                    root.transform,
                    report,
                    "_visualRootRect",
                    "_iconView",
                    "_countBadgeView",
                    "_emptyView",
                    "_hoverView",
                    "_frameView"
                );
            }
        }

        internal static void ValidateLocalReferences(
            Component owner,
            Transform scope,
            UIValidationReport report,
            params string[] fieldNames
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
                    continue;
                }

                var reference = property.objectReferenceValue;
                if (reference == null)
                {
                    report.Error(
                        owner.name,
                        fieldName,
                        "Inspectorで参照を設定してください。",
                        owner
                    );
                    continue;
                }

                var referencedTransform = GetTransform(reference);
                if (
                    referencedTransform != null
                    && referencedTransform != scope
                    && !referencedTransform.IsChildOf(scope)
                )
                {
                    report.Error(
                        owner.name,
                        fieldName,
                        $"参照先 '{reference.name}' を自分自身のPrefab階層内に設定してください。",
                        owner
                    );
                    continue;
                }

                report.Ok(owner.name, fieldName, $"{reference.name} を参照しています。", owner);
            }
        }

        private static Transform GetTransform(UnityEngine.Object reference)
        {
            return reference switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null,
            };
        }
    }

    internal sealed class UIValidationReport
    {
        private readonly string _title;
        private int _okCount;
        private int _warningCount;
        private int _errorCount;

        internal UIValidationReport(string title)
        {
            _title = title;
            Debug.Log($"[UI Validator] {_title} の検査を開始します。");
        }

        internal void Ok(
            string objectName,
            string fieldName,
            string message,
            UnityEngine.Object context
        )
        {
            _okCount++;
            Debug.Log($"[UI Validator][OK] {objectName} / {fieldName}: {message}", context);
        }

        internal void Warning(
            string objectName,
            string fieldName,
            string message,
            UnityEngine.Object context
        )
        {
            _warningCount++;
            Debug.LogWarning(
                $"[UI Validator][Warning] {objectName} / {fieldName}: {message}",
                context
            );
        }

        internal void Error(
            string objectName,
            string fieldName,
            string message,
            UnityEngine.Object context
        )
        {
            _errorCount++;
            Debug.LogError($"[UI Validator][Error] {objectName} / {fieldName}: {message}", context);
        }

        internal void Complete()
        {
            string summary =
                $"[UI Validator] {_title} 完了: OK={_okCount}, Warning={_warningCount}, Error={_errorCount}";
            if (_errorCount > 0)
                Debug.LogError(summary);
            else if (_warningCount > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }
    }
}
