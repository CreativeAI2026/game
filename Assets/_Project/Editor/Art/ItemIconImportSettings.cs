using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.Art
{
    /// <summary>
    /// アイテムアイコンPNG(Art/UI/Items 配下)のインポート設定を標準へ自動で揃える AssetPostprocessor(冪等)。
    /// Sprite / Alpha Is Transparency ON(<see cref="ItemIconTransparencyChecker"/> の期待と一致)、Mip Maps + Trilinear で縮小時のジャギを抑える。
    /// Inspector で変えても再インポートで戻る。武器プレースホルダ(他班素材)は対象外。
    /// </summary>
    public sealed class ItemIconImportSettings : AssetPostprocessor
    {
        // 対象フォルダ(アイテムアイコンの置き場)。ItemIconTransparencyChecker と揃える。
        private static readonly string[] TargetFolders =
        {
            "Assets/_Project/Art/UI/Items/Food/",
            "Assets/_Project/Art/UI/Items/Equipment/",
            "Assets/_Project/Art/UI/Items/Important/",
        };

        private void OnPreprocessTexture()
        {
            if (!IsTarget(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true; // 縮小時に事前縮小mipをサンプル → ジャギ低減
            importer.filterMode = FilterMode.Trilinear; // mip間を補間して滑らかに
        }

        private static bool IsTarget(string path)
        {
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (var folder in TargetFolders)
                if (path.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
