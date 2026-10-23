using System;
using CreativeAI.UI.CharacterUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class ModelPreviewSetup
    {
        private const string PrefabPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";

        [MenuItem("Tools/CreativeAI/UI/Setup 3D Model Previews")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var selector = root.GetComponentInChildren<CharacterWeaponSelector>(true);
                var view = Find(root.transform, "CharacterView");
                var modelArea = Find(view, "ModelArea");
                foreach (Transform child in modelArea)
                    if (child.name == "ModelLabel" || child.name == "Description")
                        child.gameObject.SetActive(false);
                var character = Preview(modelArea, "Character3DPreview", true, 0);
                var oldImage = Find(selector.transform, "WeaponPreview").GetComponent<Image>();
                oldImage.enabled = false;
                var weapon = Preview(oldImage.transform, "Weapon3DPreview", false, 20);
                var labelTransform = oldImage.transform.Find("ModelUnavailable");
                if (labelTransform == null)
                {
                    var labelObject = new GameObject(
                        "ModelUnavailable",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(TMPro.TextMeshProUGUI)
                    );
                    labelObject.transform.SetParent(oldImage.transform, false);
                    labelTransform = labelObject.transform;
                    var labelRect = (RectTransform)labelTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                    var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
                    label.font = selector.GetComponentInChildren<TMPro.TMP_Text>(true).font;
                    label.text = "3Dモデル未設定";
                    label.fontSize = 24;
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                    label.color = new Color(.65f, .7f, .75f, 1);
                    label.raycastTarget = false;
                }
                var weaponSettings = new SerializedObject(weapon);
                weaponSettings.FindProperty("_emptyLabel").objectReferenceValue =
                    labelTransform.GetComponent<TMPro.TMP_Text>();
                weaponSettings.ApplyModifiedPropertiesWithoutUndo();
                var selectorData = new SerializedObject(selector);
                selectorData.FindProperty("_weaponPreview").objectReferenceValue = null;
                selectorData.ApplyModifiedPropertiesWithoutUndo();
                var controller =
                    view.GetComponent<CharacterPreviewController>()
                    ?? view.gameObject.AddComponent<CharacterPreviewController>();
                var settings = new SerializedObject(controller);
                foreach (
                    var asset in AssetDatabase.LoadAllAssetsAtPath(
                        "Assets/_Project/Art/Models/Characters/Player/Player.fbx"
                    )
                )
                    if (asset is AnimationClip idle && idle.name == "Armature|Idle_katana")
                        settings.FindProperty("_weaponIdle").objectReferenceValue = idle;
                settings.FindProperty("_meleeAttack").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        "Assets/_Project/Art/Models/Characters/Player/Armature_Slash_01_katana.anim"
                    );
                settings.FindProperty("_characterPreview").objectReferenceValue = character;
                settings.FindProperty("_weaponPreview").objectReferenceValue = weapon;
                settings.FindProperty("_selector").objectReferenceValue = selector;
                settings.FindProperty("_weaponPanel").objectReferenceValue = selector.gameObject;
                settings.FindProperty("_weaponChangeBar").objectReferenceValue = Find(
                    view,
                    "WeaponChangeBar"
                ).gameObject;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ModelPreview Preview(Transform parent, string name, bool drag, float speed)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage),
                    typeof(ModelPreview)
                );
                go.transform.SetParent(parent, false);
                child = go.transform;
            }
            var rect = (RectTransform)child;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = drag;
            var preview = child.GetComponent<ModelPreview>();
            var settings = new SerializedObject(preview);
            settings.FindProperty("_allowDrag").boolValue = drag;
            settings.FindProperty("_rotationSpeed").floatValue = speed;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return preview;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            throw new InvalidOperationException($"Missing {name} beneath {root.name}");
        }
    }
}
