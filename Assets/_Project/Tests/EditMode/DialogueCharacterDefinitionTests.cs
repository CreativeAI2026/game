using CreativeAI.UI.ConversationUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話キャラ定義からの立ち絵(表情)解決の検証。
    /// </summary>
    public class DialogueCharacterDefinitionTests
    {
        [Test]
        public void TryResolvePortrait_ReturnsConfiguredExpressionAndMetadata()
        {
            var definition = ScriptableObject.CreateInstance<DialogueCharacterDefinition>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);

            try
            {
                TestReflection.SetField(definition, "_id", "hero");
                TestReflection.SetField(definition, "_displayName", "主人公");
                TestReflection.SetField(definition, "_side", DialoguePortraitSide.Left);
                TestReflection.SetField(
                    definition,
                    "_expressions",
                    new[]
                    {
                        new DialogueCharacterDefinition.Expression
                        {
                            PortraitKey = "hero_normal",
                            Sprite = sprite,
                            Icon = sprite,
                        },
                    }
                );

                Assert.IsTrue(definition.TryResolvePortrait("hero_normal", out var resolved));
                Assert.AreSame(sprite, resolved);
                Assert.IsTrue(
                    definition.TryResolveVisual("hero_normal", out _, out var resolvedIcon)
                );
                Assert.AreSame(sprite, resolvedIcon);
                Assert.AreEqual("hero", definition.Id);
                Assert.AreEqual("主人公", definition.DisplayName);
                Assert.AreEqual(DialoguePortraitSide.Left, definition.Side);
                Assert.IsFalse(definition.TryResolvePortrait("missing", out _));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
