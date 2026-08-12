using System;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI.Editor
{
    /// <summary>Conversation Prefabのシリアライズ参照とMissing Scriptを検証する。</summary>
    internal static class ConversationPrefabValidator
    {
        private static readonly string[] ViewReferences =
        {
            "_root",
            "_windowRoot",
            "_portrait",
            "_rightPortrait",
            "_nameText",
            "_bodyText",
            "_nextIndicator",
            "_autoModeIndicator",
            "_controlGuide",
            "_autoProgressFill",
            "_historyPanel",
            "_choiceContainer",
            "_choiceButtonTemplate",
            "_itemRewardImage",
            "_itemRewardBackdrop",
            "_weaponRewardImage",
            "_weaponRewardBackdrop",
            "_autoControlButton",
            "_skipControlButton",
            "_speedControlButton",
            "_speedControlLabel",
            "_speedToast",
            "_hideControlButton",
        };

        private static readonly string[] HistoryReferences =
        {
            "_font",
            "_panel",
            "_openButton",
            "_content",
            "_scrollRect",
            "_panelGroup",
            "_latestButton",
            "_searchField",
            "_scrollIndicator",
        };

        public static bool HasPreviewRewardReferences(GameObject prefab)
        {
            var view = prefab != null ? prefab.GetComponent<ConversationView>() : null;
            if (view == null)
                return false;

            var serializedView = new SerializedObject(view);
            return serializedView.FindProperty("_itemGetSprite").objectReferenceValue != null
                && serializedView.FindProperty("_weaponModelPrefab").objectReferenceValue != null;
        }

        public static void ValidateRequiredReferences(ConversationView view) =>
            ValidateReferences(new SerializedObject(view), ViewReferences, "ConversationView");

        public static void ValidateHistoryReferences(DialogueHistoryPanel history) =>
            ValidateReferences(new SerializedObject(history), HistoryReferences, "履歴UI");

        public static void ValidateNoMissingScripts(GameObject root)
        {
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    target.gameObject
                );
                if (missingCount > 0)
                    throw new InvalidOperationException(
                        $"[ConversationPrefabValidator] {target.name} にMissing Scriptが{missingCount}件あります。"
                    );
            }
        }

        private static void ValidateReferences(
            SerializedObject serializedObject,
            string[] requiredReferences,
            string ownerName
        )
        {
            foreach (string propertyName in requiredReferences)
            {
                var property = serializedObject.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                    throw new InvalidOperationException(
                        $"[ConversationPrefabValidator] {ownerName}の必須参照 {propertyName} が未設定です。"
                    );
            }
        }
    }
}
