using System;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class CenterCraftWarning
    {
        [MenuItem("Tools/CreativeAI/UI/Center Craft Warning %#&w")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            const string path = "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = new SerializedObject(root.GetComponent<CraftPanelController>());
                var text = (TMP_Text)controller.FindProperty("_warningText").objectReferenceValue;
                var group = (CanvasGroup)
                    controller.FindProperty("_warningCanvasGroup").objectReferenceValue;
                var rect = text.rectTransform;
                rect.SetParent(root.transform, false);
                rect.SetAsLastSibling();
                rect.anchorMin = new Vector2(.05f, .5f);
                rect.anchorMax = new Vector2(.95f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(0, 160);
                rect.localScale = Vector3.one;
                var layout =
                    text.GetComponent<LayoutElement>()
                    ?? text.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMin = 28;
                text.fontSizeMax = 40;
                text.fontSize = 40;
                text.margin = Vector4.zero;
                text.raycastTarget = false;
                group.interactable = false;
                group.blocksRaycasts = false;
                group.alpha = 0;
                text.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[CraftWarning] Warning is centered over the screen and excluded from the crafting layout."
            );
        }
    }
}
