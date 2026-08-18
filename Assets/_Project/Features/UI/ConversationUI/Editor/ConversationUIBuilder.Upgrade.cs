using UnityEditor;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI.Editor
{
    public static partial class ConversationUIBuilder
    {
        [MenuItem("Tools/CreativeAI/Upgrade Conversation UI If Needed")]
        public static void UpgradePrefabLayoutIfNeeded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (IsCurrentPrefab(prefab))
            {
                Debug.Log("[ConversationUIBuilder] ConversationView Prefab は最新です。");
                return;
            }

            ConversationSpriteImporter.ImportAsSprite(WindowPng);
            ConversationSpriteImporter.ImportAsSprite(ContinueButtonPng);
            ConversationSpriteImporter.ImportAsSprite(ChoiceButtonPng);
            ConversationSpriteImporter.ImportAsSprite(ItemPreviewPng);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ConversationUIBuilder] ConversationView Prefab を最新UI構成へ更新しました。"
            );
        }

        private static bool IsCurrentPrefab(GameObject prefab) =>
            prefab != null
            && prefab.transform.Find("ContextGuide") != null
            && prefab.transform.Find("ItemRewardBackdrop") != null
            && prefab.transform.Find("AUTOAButton") != null
            && prefab.transform.Find("DialogueHistoryPanel") != null
            && prefab.transform.Find("Layout/_System/ConversationArchiveV11") != null
            && ConversationPrefabValidator.HasPreviewRewardReferences(prefab);
    }
}
