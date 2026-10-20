using System;
using System.Linq;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class FixLoadoutParameterWidths
    {
        [MenuItem("Tools/CreativeAI/UI/Fix Loadout Widths And HUD Icons %#&l")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            foreach (
                string path in new[]
                {
                    "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab",
                    "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab",
                }
            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var character =
                        root.name == "CharacterPanel"
                            ? root.transform
                            : root.transform.Find("CharacterUI/CharacterPanel");
                    var parameters = character.Find(
                        "ContentArea/LoadoutView/InventoryArea/DetailArea/ParamaterRow"
                    );
                    foreach (
                        var row in parameters.GetComponentsInChildren<HorizontalLayoutGroup>(true)
                    )
                    {
                        if (
                            row.transform.Find("Label") == null
                            || row.transform.Find("Value") == null
                        )
                            continue;
                        // Explicit layout hints override TMP's text-dependent preferred width.
                        SetWidth(row.gameObject);
                        SetWidth(row.transform.Find("Label").gameObject);
                        SetWidth(row.transform.Find("Value").gameObject);
                    }
                    if (root.name == "UIRoot")
                    {
                        var hud = root.GetComponentInChildren<HudIconBar>(true);
                        foreach (var name in new[] { "Character", "Inventory", "Save" })
                        {
                            var image = hud.transform.Find(name + "Button").GetComponent<Image>();
                            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                                "Assets/_Project/Art/UI/HUD/IconBar/Icon_" + name + ".png"
                            );
                            if (sprite == null)
                                throw new InvalidOperationException("Missing HUD icon: " + name);
                            image.sprite = sprite;
                            image.overrideSprite = null;
                            image.color = Color.white;
                            image.preserveAspect = true;
                            image.enabled = true;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[LoadoutWidths] Saved stable parameter widths and restored Character, Inventory and Save HUD icons."
            );
        }

        private static void SetWidth(GameObject target)
        {
            var layout =
                target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minWidth = 0;
            layout.preferredWidth = 0;
            layout.flexibleWidth = 1;
        }

        [MenuItem("Tools/CreativeAI/UI/Verify Loadout Parameter Widths %#&v")]
        public static void Verify()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Start Play mode first.");
            var router = UnityEngine.Object.FindAnyObjectByType<UiRouter>();
            var root = router.transform;
            var parameters = root.Find(
                "CharacterUI/CharacterPanel/ContentArea/LoadoutView/InventoryArea/DetailArea/ParamaterRow"
            );
            var values = parameters
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t.name == "Value")
                .ToArray();
            var text = values.Select(t => t.text).ToArray();
            var detail = (RectTransform)parameters.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
            var widths = values
                .Select(t => ((RectTransform)t.transform.parent).rect.width)
                .ToArray();
            try
            {
                foreach (var value in values)
                    value.text = "+999.99%";
                LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
                for (int i = 0; i < values.Length; i++)
                    if (
                        Mathf.Abs(
                            ((RectTransform)values[i].transform.parent).rect.width - widths[i]
                        ) > .01f
                    )
                        throw new InvalidOperationException(
                            "Parameter width changed: " + values[i].transform.parent.name
                        );
                Debug.Log(
                    $"[LoadoutWidths] Verified {values.Length} parameter widths stay fixed when values change to +999.99%."
                );
            }
            finally
            {
                for (int i = 0; i < values.Length; i++)
                    values[i].text = text[i];
                LayoutRebuilder.ForceRebuildLayoutImmediate(detail);
            }
        }
    }
}
