using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    [InitializeOnLoad]
    internal static class DynamicFontAssetCacheCleaner
    {
        private const string FontGuid = "b99b78d9239e217499db95a88bb2a233";

        // Clear Dynamic Data と同じ処理を使い、文字間隔などのフォント機能は保持する。
        private static readonly MethodInfo ClearDynamicData = typeof(TMP_FontAsset).GetMethod(
            "ClearCharacterAndGlyphTablesInternal",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        static DynamicFontAssetCacheCleaner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.delayCall -= ClearAfterPlay;
            EditorApplication.delayCall += ClearAfterPlay;
        }

        private static void ClearAfterPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(FontGuid)
            );
            if (
                font == null
                || font.atlasPopulationMode != AtlasPopulationMode.Dynamic
                || !new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue
                || (
                    font.glyphTable.Count == 0
                    && font.characterTable.Count == 0
                    && font.atlasTexture != null
                    && font.atlasTexture.width <= 1
                )
            )
                return;

            if (ClearDynamicData == null)
            {
                Debug.LogError("TMP の Clear Dynamic Data 処理が見つかりません。", font);
                return;
            }

            ClearDynamicData.Invoke(font, null);
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }
    }
}
