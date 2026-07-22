using System;
using System.IO;
using CreativeAI.UI;
using CreativeAI.UI.InventoryUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    [InitializeOnLoad]
    public static class RevolverTabSetupHelper
    {
        private const string SetupRequestPath = "Temp/RevolverTabSetup/integrate.request";
        private const string SetupResultPath = "Temp/RevolverTabSetup/integrate-result.json";
        private const string ValidateRequestPath = "Temp/RevolverTabSetup/validate.request";
        private const string ValidateResultPath = "Temp/RevolverTabSetup/validate-result.json";
        private const string ItemPrefabPath =
            "Assets/_Project/Features/UI/Common/Prefabs/RevolverTabItem.prefab";
        private const string TabButtonPrefabPath =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/TabButton.prefab";
        private const string CharacterPanelPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
        private const string DefinitionDirectory =
            "Assets/_Project/Features/Inventory/Data/TabDefinition/";

        private static bool _processing;

        static RevolverTabSetupHelper()
        {
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }

        [MenuItem("CONTEXT/RevolverTabItemView/Auto Assign")]
        private static void AutoAssignItem(MenuCommand command)
        {
            AutoAssign((RevolverTabItemView)command.context, true);
        }

        [MenuItem("CONTEXT/RevolverTabGroup/Auto Assign")]
        private static void AutoAssignGroup(MenuCommand command)
        {
            AutoAssign((RevolverTabGroup)command.context, true);
        }

        [MenuItem("CONTEXT/RevolverTabItemView/Validate Configuration")]
        private static void ValidateItem(MenuCommand command)
        {
            Validate((RevolverTabItemView)command.context, true);
        }

        [MenuItem("CONTEXT/RevolverTabGroup/Validate Configuration")]
        private static void ValidateGroup(MenuCommand command)
        {
            Validate((RevolverTabGroup)command.context, true);
        }

        [MenuItem("GameObject/CreativeAI UI/Attach Revolver Tab Item View", false, 10)]
        private static void AttachItemView(MenuCommand command)
        {
            var target = command.context as GameObject ?? Selection.activeGameObject;
            if (target == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(target, "Attach Revolver Tab Item View");
            if (target.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(target);
            var item =
                target.GetComponent<RevolverTabItemView>()
                ?? Undo.AddComponent<RevolverTabItemView>(target);
            AutoAssign(item, true);
        }

        [MenuItem("Tools/CreativeAI/UI/Revolver/Create Item Prefab")]
        public static void CreateItemPrefab()
        {
            var tabButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabButtonPrefabPath);
            if (tabButtonPrefab == null)
                throw new InvalidOperationException(
                    $"TabButton Prefab not found: {TabButtonPrefabPath}"
                );

            var root = new GameObject(
                "RevolverTabItem",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(RevolverTabItemView)
            );
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(100f, 100f);
                rootRect.localScale = Vector3.one;

                var tabButtonObject = (GameObject)PrefabUtility.InstantiatePrefab(tabButtonPrefab);
                tabButtonObject.transform.SetParent(root.transform, false);
                tabButtonObject.transform.localScale = Vector3.one;

                var tabButton = tabButtonObject.GetComponent<TabButton>();
                var hover = tabButtonObject.GetComponent<HoverScaleOnPointer>();
                var visualRoot = tabButtonObject.transform.Find("VisualRoot") as RectTransform;
                ConfigureHover(hover, visualRoot);

                var item = root.GetComponent<RevolverTabItemView>();
                var itemObject = new SerializedObject(item);
                itemObject.FindProperty("_tabButton").objectReferenceValue = tabButton;
                itemObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/CreativeAI/UI/Revolver/Integrate Weapon UI")]
        public static void IntegrateWeaponUi()
        {
            var itemPrefab = AssetDatabase.LoadAssetAtPath<RevolverTabItemView>(ItemPrefabPath);
            var definitions = new[]
            {
                LoadDefinition("SwordTabDefinition"),
                LoadDefinition("BowTabDefinition"),
                LoadDefinition("KamaTabDefinition"),
            };
            if (itemPrefab == null || Array.Exists(definitions, definition => definition == null))
                throw new InvalidOperationException(
                    "Revolver Item Prefab or weapon definitions are missing."
                );

            var root = PrefabUtility.LoadPrefabContents(CharacterPanelPath);
            try
            {
                var weaponView = FindUnique(root.transform, "WeaponView");
                if (weaponView == null)
                    throw new InvalidOperationException("WeaponView was not found uniquely.");

                var area = weaponView.Find("RevolverTabArea") as RectTransform;
                if (area == null)
                    area = CreateRect("RevolverTabArea", weaponView);
                ConfigureArea(area);

                var itemRoot = area.Find("ItemRoot") as RectTransform;
                if (itemRoot == null)
                    itemRoot = CreateRect("ItemRoot", area);
                ConfigureItemRoot(itemRoot);

                var group =
                    area.GetComponent<RevolverTabGroup>()
                    ?? area.gameObject.AddComponent<RevolverTabGroup>();
                ConfigureGroup(group, itemPrefab, itemRoot, definitions);

                var oldGroup = FindUnique(weaponView, "WeaponTabGroup");
                if (oldGroup != null)
                    oldGroup.gameObject.SetActive(false);

                var controller =
                    weaponView.GetComponent<CreativeAI.UI.CharacterUI.WeaponTabViewController>();
                if (controller == null)
                    throw new InvalidOperationException("WeaponTabViewController was not found.");
                var controllerObject = new SerializedObject(controller);
                controllerObject.FindProperty("_revolverTabGroup").objectReferenceValue = group;
                controllerObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static bool AutoAssign(RevolverTabItemView item, bool logWarnings)
        {
            if (item == null)
                return false;

            var candidates = item.GetComponentsInChildren<TabButton>(true);
            if (candidates.Length != 1)
            {
                if (logWarnings)
                    Debug.LogWarning(
                        $"{nameof(RevolverTabItemView)} requires exactly one TabButton; found {candidates.Length}.",
                        item
                    );
                return false;
            }

            Undo.RecordObject(item, "Auto Assign Revolver Tab Item");
            var serializedObject = new SerializedObject(item);
            serializedObject.FindProperty("_tabButton").objectReferenceValue = candidates[0];
            serializedObject.ApplyModifiedProperties();

            var hover = candidates[0].GetComponent<HoverScaleOnPointer>();
            var visualRoot = candidates[0].transform.Find("VisualRoot") as RectTransform;
            ConfigureHover(hover, visualRoot);
            return Validate(item, logWarnings);
        }

        public static bool AutoAssign(RevolverTabGroup group, bool logWarnings)
        {
            if (group == null)
                return false;

            var roots = FindDirectChildren(group.transform, "ItemRoot");
            var itemViews = group.GetComponentsInChildren<RevolverTabItemView>(true);
            if (roots.Length != 1 || itemViews.Length != 1)
            {
                if (logWarnings)
                    Debug.LogWarning(
                        $"{nameof(RevolverTabGroup)} requires one ItemRoot and one Item View candidate; found {roots.Length}/{itemViews.Length}.",
                        group
                    );
                return false;
            }

            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(itemViews[0]);
            if (prefab == null)
                prefab = itemViews[0];

            Undo.RecordObject(group, "Auto Assign Revolver Tab Group");
            var serializedObject = new SerializedObject(group);
            serializedObject.FindProperty("_itemRoot").objectReferenceValue = roots[0];
            serializedObject.FindProperty("_itemPrefab").objectReferenceValue = prefab;
            serializedObject.ApplyModifiedProperties();
            return Validate(group, logWarnings);
        }

        public static bool Validate(RevolverTabItemView item, bool logWarnings)
        {
            bool valid =
                item != null
                && item.GetComponent<CanvasGroup>() != null
                && item.TabButton != null
                && item.TabButton.Button != null;
            if (!valid && logWarnings)
                Debug.LogWarning("Revolver Tab Item configuration is incomplete.", item);
            return valid;
        }

        public static bool Validate(RevolverTabGroup group, bool logWarnings)
        {
            if (group == null)
                return false;

            var serializedObject = new SerializedObject(group);
            var layout = serializedObject.FindProperty("_layout");
            int placement = layout.FindPropertyRelative("_placement").enumValueIndex;
            bool valid =
                serializedObject.FindProperty("_itemRoot").objectReferenceValue != null
                && serializedObject.FindProperty("_itemPrefab").objectReferenceValue != null
                && placement >= (int)RevolverArcPlacement.Top
                && placement <= (int)RevolverArcPlacement.Right
                && layout.FindPropertyRelative("_tangentRadius").floatValue >= 0f
                && layout.FindPropertyRelative("_arcDepth").floatValue >= 0f;
            if (!valid && logWarnings)
                Debug.LogWarning("Revolver Tab Group configuration is incomplete.", group);
            return valid;
        }

        public static bool ApplyPlacementToRoot(RevolverTabGroup group)
        {
            if (group == null || group.transform is not RectTransform root)
                return false;

            var serializedObject = new SerializedObject(group);
            var layout = serializedObject.FindProperty("_layout");
            var placement = (RevolverArcPlacement)
                layout.FindPropertyRelative("_placement").enumValueIndex;
            Vector2 anchor = placement switch
            {
                RevolverArcPlacement.Top => new Vector2(0.5f, 1f),
                RevolverArcPlacement.Bottom => new Vector2(0.5f, 0f),
                RevolverArcPlacement.Left => new Vector2(0f, 0.5f),
                _ => new Vector2(1f, 0.5f),
            };

            Undo.RecordObject(root, "Apply Revolver Arc Placement");
            root.anchorMin = anchor;
            root.anchorMax = anchor;
            root.pivot = anchor;
            EditorUtility.SetDirty(root);
            return true;
        }

        private static void ProcessRequest()
        {
            if (
                _processing
                || (!File.Exists(SetupRequestPath) && !File.Exists(ValidateRequestPath))
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode
            )
                return;

            _processing = true;
            if (File.Exists(ValidateRequestPath))
            {
                ProcessValidationRequest();
                return;
            }

            var result = new SetupResult();
            try
            {
                CreateItemPrefab();
                IntegrateWeaponUi();
                result.status = "passed";
            }
            catch (Exception exception)
            {
                result.status = "failed";
                result.message = exception.ToString();
                Debug.LogException(exception);
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SetupResultPath));
                File.WriteAllText(SetupResultPath, JsonUtility.ToJson(result, true));
                File.Delete(SetupRequestPath);
                _processing = false;
            }
        }

        private static void ProcessValidationRequest()
        {
            var result = new ValidationResult();
            Application.LogCallback callback = (condition, stackTrace, type) =>
            {
                if (!condition.Contains("[UI Validator]"))
                    return;

                if (type == LogType.Error || type == LogType.Exception)
                {
                    result.errors++;
                    result.messages.Add(condition);
                }
                else if (type == LogType.Warning)
                {
                    result.warnings++;
                    result.messages.Add(condition);
                }
            };

            Application.logMessageReceived += callback;
            try
            {
                Area01UIValidator.ValidateAllFromMenu();
                result.status = result.errors == 0 ? "passed" : "failed";
            }
            catch (Exception exception)
            {
                result.status = "failed";
                result.errors++;
                result.messages.Add(exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= callback;
                Directory.CreateDirectory(Path.GetDirectoryName(ValidateResultPath));
                File.WriteAllText(ValidateResultPath, JsonUtility.ToJson(result, true));
                File.Delete(ValidateRequestPath);
                _processing = false;
            }
        }

        private static void ConfigureHover(HoverScaleOnPointer hover, RectTransform visualRoot)
        {
            if (hover == null)
                return;

            Undo.RecordObject(hover, "Configure Revolver Tab Hover");
            var serializedObject = new SerializedObject(hover);
            serializedObject.FindProperty("_hoverScaleEnabled").boolValue = false;
            serializedObject.FindProperty("_bounceEnabled").boolValue = false;
            serializedObject.FindProperty("_targetRect").objectReferenceValue = visualRoot;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGroup(
            RevolverTabGroup group,
            RevolverTabItemView itemPrefab,
            RectTransform itemRoot,
            TabDefinition[] definitions
        )
        {
            var serializedObject = new SerializedObject(group);
            serializedObject.FindProperty("_itemPrefab").objectReferenceValue = itemPrefab;
            serializedObject.FindProperty("_itemRoot").objectReferenceValue = itemRoot;
            serializedObject.FindProperty("_initialIndex").intValue = 0;
            serializedObject.FindProperty("_moveDuration").floatValue = 0.25f;
            serializedObject.FindProperty("_loop").boolValue = true;

            var entries = serializedObject.FindProperty("_entries");
            entries.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_definition").objectReferenceValue = definitions[i];
                entry.FindPropertyRelative("_view").objectReferenceValue = null;
            }

            var layout = serializedObject.FindProperty("_layout");
            layout.FindPropertyRelative("_visibleItemCount").intValue = 3;
            layout.FindPropertyRelative("_tangentRadius").floatValue = 180f;
            layout.FindPropertyRelative("_arcDepth").floatValue = 80f;
            layout.FindPropertyRelative("_maxAngle").floatValue = 60f;
            layout.FindPropertyRelative("_placement").enumValueIndex = (int)
                RevolverArcPlacement.Top;
            layout.FindPropertyRelative("_selectedScale").floatValue = 1.2f;
            layout.FindPropertyRelative("_edgeScale").floatValue = 0.6f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureArea(RectTransform area)
        {
            area.anchorMin = new Vector2(0.5f, 0.5f);
            area.anchorMax = new Vector2(0.5f, 0.5f);
            area.pivot = new Vector2(0.5f, 0.5f);
            area.anchoredPosition = new Vector2(0f, 170f);
            area.sizeDelta = new Vector2(500f, 220f);
            area.localScale = Vector3.one;
        }

        private static void ConfigureItemRoot(RectTransform itemRoot)
        {
            itemRoot.anchorMin = Vector2.one * 0.5f;
            itemRoot.anchorMax = Vector2.one * 0.5f;
            itemRoot.pivot = Vector2.one * 0.5f;
            itemRoot.anchoredPosition = Vector2.zero;
            itemRoot.sizeDelta = Vector2.zero;
            itemRoot.localScale = Vector3.one;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TabDefinition LoadDefinition(string name) =>
            AssetDatabase.LoadAssetAtPath<TabDefinition>(DefinitionDirectory + name + ".asset");

        private static Transform FindUnique(Transform root, string name)
        {
            Transform found = null;
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != name)
                    continue;
                if (found != null)
                    return null;
                found = candidate;
            }
            return found;
        }

        private static RectTransform[] FindDirectChildren(Transform root, string name)
        {
            var matches = new System.Collections.Generic.List<RectTransform>();
            foreach (Transform child in root)
            {
                if (child.name == name && child is RectTransform rect)
                    matches.Add(rect);
            }
            return matches.ToArray();
        }

        [Serializable]
        private sealed class SetupResult
        {
            public string status;
            public string message;
        }

        [Serializable]
        private sealed class ValidationResult
        {
            public string status;
            public int errors;
            public int warnings;
            public System.Collections.Generic.List<string> messages = new();
        }
    }

    [CustomEditor(typeof(RevolverTabGroup))]
    internal sealed class RevolverTabGroupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Apply Placement To Root"))
                RevolverTabSetupHelper.ApplyPlacementToRoot((RevolverTabGroup)target);
        }
    }
}
