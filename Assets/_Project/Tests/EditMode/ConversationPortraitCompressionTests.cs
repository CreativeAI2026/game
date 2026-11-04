using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話の立ち絵が4の倍数のサイズで、DXT5/BC7に圧縮されているかの検証。
    /// 圧縮が効かず非圧縮(RGBA32)のままメモリに載り続けるのを防ぐ。
    /// </summary>
    public class ConversationPortraitCompressionTests
    {
        private const string PortraitRoot = "Assets/_Project/Art/UI/Conversation/Character";

        [Test]
        public void Portraits_AreBlockCompressionCompatible()
        {
            string[] portraitPaths = AssetDatabase
                .FindAssets("t:Texture2D", new[] { PortraitRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    path.Contains("/Portraits/", StringComparison.Ordinal)
                    && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            // 枚数は立ち絵を足すと変わるので固定しない。フォルダを移して0枚になったときだけ気づければよい
            Assert.IsNotEmpty(portraitPaths, $"{PortraitRoot} に立ち絵が見つからない");

            foreach (string path in portraitPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Assert.AreEqual(0, width % 4, path);
                Assert.AreEqual(0, height % 4, path);
                Assert.AreEqual(
                    TextureImporterCompression.Compressed,
                    importer.textureCompression,
                    path
                );
                Assert.IsFalse(importer.mipmapEnabled, path);

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, path);
                CollectionAssert.Contains(
                    new[] { TextureFormat.DXT5, TextureFormat.BC7 },
                    texture.format,
                    $"{path}: {texture.format}"
                );

                long blockCompressedBytes = ((width + 3L) / 4L) * ((height + 3L) / 4L) * 16L;
                TestContext.WriteLine(
                    $"{path}: {width}x{height}, {texture.format}, {blockCompressedBytes:N0} bytes"
                );
            }
        }
    }
}
