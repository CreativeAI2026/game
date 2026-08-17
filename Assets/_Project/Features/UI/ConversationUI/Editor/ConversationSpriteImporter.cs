using UnityEditor;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI.Editor
{
    /// <summary>Conversation UI用画像のSprite import設定を統一する。</summary>
    internal static class ConversationSpriteImporter
    {
        public static void ImportAsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError(
                    $"[ConversationSpriteImporter] TextureImporter が取れません: {path}"
                );
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
