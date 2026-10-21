using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class RepairCharacterUi
    {
        private const string Path =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";

        [MenuItem("Tools/CreativeAI/UI/Configure Loadout Appearance %#&a")]
        public static void ConfigureAppearance()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            foreach (
                var path in new[] { Path, "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab" }
            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var character =
                        root.name == "CharacterPanel"
                            ? root.transform
                            : root.transform.Find("CharacterUI/CharacterPanel");
                    var loadout = character.Find("ContentArea/LoadoutView");
                    foreach (var area in new[] { "QuickItemArea/SlotArea", "EquipArea/SlotArea" })
                    {
                        var layout = loadout.Find(area).GetComponent<HorizontalLayoutGroup>();
                        layout.childScaleWidth = false;
                        layout.childScaleHeight = false;
                    }
                    var controller = new SerializedObject(
                        loadout.GetComponent<CreativeAI.UI.CharacterUI.LoadoutPanelController>()
                    );
                    controller.FindProperty("_slotAppearDuration").floatValue = .2f;
                    controller.FindProperty("_slotAppearStagger").floatValue = .05f;
                    controller.FindProperty("_animateDetailText").boolValue = true;
                    controller.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[CharacterUI] Loadout slot appearance and typewriter settings saved in prefabs."
            );
        }

        [MenuItem("Tools/CreativeAI/UI/Repair Character Images %#&k")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            var buttons = new[] { "BLADE", "BOW", "SCYTHE", "HAMMER" };
            var files = new[] { "CutterBlade", "Bow", "Scythe", "Gastrointestinal Hammer" };
            var dataNames = new[] { "Sword", "Bow", "Scythe", "Hammer" };
            var icons = files
                .Select(file =>
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/_Project/Art/UI/HUD/WeaponIcon/" + file + ".png"
                    )
                )
                .ToArray();
            if (icons.Any(icon => icon == null))
                throw new System.InvalidOperationException("Missing weapon icon.");
            for (int i = 0; i < icons.Length; i++)
            {
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    "Assets/_Project/Features/Player/Data/Weapons/" + dataNames[i] + ".asset"
                );
                data.icon = icons[i];
                EditorUtility.SetDirty(data);
            }
            foreach (
                var path in new[] { Path, "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab" }
            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var character =
                        root.name == "CharacterPanel"
                            ? root.transform
                            : root.transform.Find("CharacterUI/CharacterPanel");
                    for (int i = 0; i < icons.Length; i++)
                        Assign(
                            character
                                .Find(
                                    "ContentArea/CharacterView/WeaponChangeBar/"
                                        + buttons[i]
                                        + "/WeaponIcon"
                                )
                                .GetComponent<Image>(),
                            icons[i]
                        );
                    Assign(
                        character
                            .Find("ContentArea/CharacterView/WeaponPanel/TitleArea/Favicon")
                            .GetComponent<Image>(),
                        AssetDatabase.LoadAssetAtPath<Sprite>(
                            "Assets/_Project/Art/UI/Character/TabIcon/Icon_Tab_Weapon.png"
                        )
                    );
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[CharacterUI] Restored four weapon icons and weapon title favicon in CharacterPanel and UIRoot prefabs."
            );
        }

        private static void Assign(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.overrideSprite = null;
            image.color = Color.white;
            image.preserveAspect = true;
            image.enabled = true;
        }

        [MenuItem("Tools/CreativeAI/UI/Inspect Character UI %#&j")]
        public static void Inspect()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            string Hierarchy(Transform t) =>
                t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
            File.WriteAllLines(
                "Temp/CharacterUiBindings.txt",
                root.GetComponentsInChildren<Transform>(true)
                    .Select(t =>
                    {
                        var image = t.GetComponent<Image>();
                        return Hierarchy(t)
                            + (
                                image == null
                                    ? ""
                                    : $" | Image={(image.sprite != null ? image.sprite.name : "NULL")}, asset={AssetDatabase.GetAssetPath(image.sprite)}, enabled={image.enabled}, color={image.color}"
                            );
                    })
            );
            if (EditorApplication.isPlaying)
                Object.FindAnyObjectByType<UiRouter>().Open(UiRouter.UiId.Character);
            Debug.Log("[CharacterUI] Bindings written to Temp/CharacterUiBindings.txt");
        }
    }
}
