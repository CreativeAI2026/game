using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 日本語フォントの動的キャッシュ（使った文字のグリフ）を、フォントが保存される直前に消す。
    /// 文字はプレイ中だけでなく EditMode テストやエディット中の UI 表示でも増えるため、
    /// プレイ終了時ではなく保存の直前で消し、どの経路でもキャッシュ入りのままコミットされないようにする。
    /// </summary>
    internal sealed class DynamicFontAssetCacheCleaner : AssetModificationProcessor
    {
        private const string FontGuid = "b99b78d9239e217499db95a88bb2a233";

        // Clear Dynamic Data と同じ処理を使い、文字間隔などのフォント機能は保持する。
        private static readonly MethodInfo ClearDynamicData = typeof(TMP_FontAsset).GetMethod(
            "ClearCharacterAndGlyphTablesInternal",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        private static string[] OnWillSaveAssets(string[] paths)
        {
            string fontPath = AssetDatabase.GUIDToAssetPath(FontGuid);
            if (!string.IsNullOrEmpty(fontPath) && Array.IndexOf(paths, fontPath) >= 0)
            {
                ClearCachedGlyphs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath));
            }

            return paths;
        }

        private static void ClearCachedGlyphs(TMP_FontAsset font)
        {
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
        }
    }
}
