using UnityEditor;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI.Editor
{
    public static partial class ConversationUIBuilder
    {
        [InitializeOnLoadMethod]
        private static void UpgradePrefabLayoutIfNeeded()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            QueuePrefabUpgrade();
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                QueuePrefabUpgrade();
        }

        private static void QueuePrefabUpgrade()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (
                    prefab != null
                    && prefab.transform.Find("ContextGuide") != null
                    && prefab.transform.Find("ItemRewardBackdrop") != null
                    && prefab.transform.Find("AUTOAButton") != null
                    && prefab.transform.Find("DialogueHistoryPanel") != null
                    && prefab.transform.Find("Layout/_System/ConversationArchiveV11") != null
                    && ConversationPrefabValidator.HasPreviewRewardReferences(prefab)
                )
                    return;

                ConversationSpriteImporter.ImportAsSprite(WindowPng);
                ConversationSpriteImporter.ImportAsSprite(ContinueButtonPng);
                ConversationSpriteImporter.ImportAsSprite(ChoiceButtonPng);
                ConversationSpriteImporter.ImportAsSprite(ItemPreviewPng);
                BuildPrefab();
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "[ConversationUIBuilder] ConversationView Prefab を最新UI構成へ更新しました。"
                );
            };
        }
    }
}
