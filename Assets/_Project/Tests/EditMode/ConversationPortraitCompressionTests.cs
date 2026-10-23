using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class ConversationPortraitCompressionTests
    {
        private const string PortraitRoot = "Assets/_Project/Art/UI/Conversation/Character";
        private const int ExpectedPortraitCount = 14;
        private const int ExpectedWidth = 924;
        private const int ExpectedHeight = 1772;

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

            Assert.AreEqual(ExpectedPortraitCount, portraitPaths.Length);

            foreach (string path in portraitPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Assert.AreEqual(ExpectedWidth, width, path);
                Assert.AreEqual(ExpectedHeight, height, path);
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
