using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 立ち絵キーからキャラ定義の表情・名前・配置を引き、見つからなければ既定表示に戻す処理の検証。
    /// </summary>
    public class DialoguePortraitResolverTests
    {
        private Texture2D _texture;
        private Sprite _heroSprite;
        private Sprite _defaultSprite;
        private DialogueCharacterDefinition _hero;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(2, 2);
            _heroSprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            _defaultSprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);

            _hero = ScriptableObject.CreateInstance<DialogueCharacterDefinition>();
            TestReflection.SetField(_hero, "_displayName", "主人公");
            TestReflection.SetField(_hero, "_side", DialoguePortraitSide.Right);
            TestReflection.SetField(_hero, "_themeColor", Color.red);
            TestReflection.SetField(_hero, "_portraitOffset", new Vector2(3f, 4f));
            TestReflection.SetField(
                _hero,
                "_expressions",
                new[]
                {
                    new DialogueCharacterDefinition.Expression
                    {
                        PortraitKey = "hero_normal",
                        Sprite = _heroSprite,
                        Icon = _heroSprite,
                    },
                }
            );
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hero);
            Object.DestroyImmediate(_heroSprite);
            Object.DestroyImmediate(_defaultSprite);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void KnownKey_ReturnsCharacterVisualAndMetadata()
        {
            var resolved = DialoguePortraitResolver.Resolve(
                "hero_normal",
                new[] { _hero },
                _defaultSprite,
                DialoguePortraitSide.Left
            );

            Assert.AreSame(_heroSprite, resolved.Sprite);
            Assert.AreSame(_heroSprite, resolved.Icon);
            Assert.AreEqual(DialoguePortraitSide.Right, resolved.Side);
            Assert.AreEqual("主人公", resolved.DisplayName);
            Assert.AreEqual(Color.red, resolved.ThemeColor);
            Assert.AreEqual(new Vector2(3f, 4f), resolved.Offset);
        }

        [Test]
        public void KnownKey_SkipsNullEntriesAndFindsLaterCharacter()
        {
            var resolved = DialoguePortraitResolver.Resolve(
                "hero_normal",
                new DialogueCharacterDefinition[] { null, _hero },
                _defaultSprite,
                DialoguePortraitSide.Left
            );

            Assert.AreSame(_heroSprite, resolved.Sprite);
        }

        [TestCase("missing")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownOrEmptyKey_FallsBackToDefaults(string key)
        {
            var resolved = DialoguePortraitResolver.Resolve(
                key,
                new[] { _hero },
                _defaultSprite,
                DialoguePortraitSide.Left
            );

            Assert.AreSame(_defaultSprite, resolved.Sprite);
            Assert.AreSame(_defaultSprite, resolved.Icon);
            Assert.AreEqual(DialoguePortraitSide.Left, resolved.Side);
            Assert.AreEqual(string.Empty, resolved.DisplayName);
            Assert.AreEqual(Vector2.zero, resolved.Offset);
            Assert.IsNull(resolved.TypingSound);
        }

        [Test]
        public void NullCharacterList_FallsBackToDefaults()
        {
            var resolved = DialoguePortraitResolver.Resolve(
                "hero_normal",
                null,
                _defaultSprite,
                DialoguePortraitSide.Right
            );

            Assert.AreSame(_defaultSprite, resolved.Sprite);
            Assert.AreEqual(DialoguePortraitSide.Right, resolved.Side);
        }
    }
}
