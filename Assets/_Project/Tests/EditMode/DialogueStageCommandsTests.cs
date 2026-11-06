using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 背景の演出面(暗転・フラッシュ)と、演出コマンドの振り分けの検証。
    /// フェードは秒数 0 で呼び、時間経過に頼らず結果だけを見る。
    /// </summary>
    public class DialogueStageCommandsTests
    {
        private GameObject _canvas;
        private RectTransform _conversationRoot;
        private DialogueBackdropPresenter _backdrop;
        private DialogueStageCommands _stage;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform));
            var rootObject = new GameObject("ConversationRoot", typeof(RectTransform));
            _conversationRoot = (RectTransform)rootObject.transform;
            _conversationRoot.SetParent(_canvas.transform, false);
            _backdrop = new DialogueBackdropPresenter(_conversationRoot);
            _stage = new DialogueStageCommands(_backdrop);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_canvas);

        // コルーチンの中で別のコルーチンを yield return している(ゲームでは Unity が内側も進める)ので、
        // 内側が返ってきたらそれを先に最後まで進める。
        private static void Drive(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int guard = 0; guard < 1000 && stack.Count > 0; guard++)
            {
                if (!stack.Peek().MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (stack.Peek().Current is IEnumerator inner)
                    stack.Push(inner);
            }
        }

        private Image Find(string name)
        {
            Transform child = _canvas.transform.Find(name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        [Test]
        public void Black_CoversInstantly_AndSitsBehindConversationRoot()
        {
            Drive(_stage.Create("background.black", null));

            var fill = Find("BackdropFill");
            Assert.IsNotNull(fill);
            Assert.IsTrue(fill.gameObject.activeSelf);
            Assert.AreEqual(1f, fill.color.a, 0.001f);
            Assert.Less(fill.transform.GetSiblingIndex(), _conversationRoot.GetSiblingIndex());
        }

        [Test]
        public void Clear_HidesTheFill()
        {
            Drive(_stage.Create("background.black", null));

            Drive(_stage.Create("background.black.clear", "0"));

            Assert.IsFalse(Find("BackdropFill").gameObject.activeSelf);
        }

        [Test]
        public void Clear_WithoutFill_DoesNothing()
        {
            Assert.DoesNotThrow(() => Drive(_stage.Create("background.clear", "0")));
            Assert.IsNull(Find("BackdropFill"));
        }

        [Test]
        public void Fill_UsesGivenColor()
        {
            Drive(_stage.Create("background.fill", "#FF0000,0"));

            var color = Find("BackdropFill").color;
            Assert.AreEqual(1f, color.r, 0.001f);
            Assert.AreEqual(0f, color.g, 0.001f);
            Assert.AreEqual(1f, color.a, 0.001f);
        }

        [Test]
        public void Fill_WithBadColor_FallsBackToBlack()
        {
            Drive(_stage.Create("background.fill", "not-a-color,0"));

            var color = Find("BackdropFill").color;
            Assert.AreEqual(0f, color.r, 0.001f);
            Assert.AreEqual(1f, color.a, 0.001f);
        }

        [Test]
        public void Flash_SitsInFrontOfConversationRoot_AndDisappears()
        {
            Drive(_stage.Create("background.flash", "white,0"));

            var flash = Find("BackdropFlash");
            Assert.IsNotNull(flash);
            Assert.IsFalse(flash.gameObject.activeSelf);
            Assert.Greater(flash.transform.GetSiblingIndex(), _conversationRoot.GetSiblingIndex());
        }

        [Test]
        public void Reset_HidesEveryLayer()
        {
            Drive(_stage.Create("background.black", null));
            Drive(_stage.Create("background.flash", "white,0"));

            _backdrop.Reset();

            Assert.IsFalse(Find("BackdropFill").gameObject.activeSelf);
        }

        [TestCase("wait")]
        [TestCase("window.hide")]
        [TestCase("portrait.show")]
        [TestCase("camera.pan")]
        public void Create_ReturnsNull_ForCommandsItDoesNotOwn(string command)
        {
            Assert.IsNull(_stage.Create(command, "x"));
        }

        [TestCase("background.black")]
        [TestCase("background.black.fill")]
        [TestCase("background.black.clear")]
        [TestCase("background.color")]
        [TestCase("background.fill")]
        [TestCase("background.clear")]
        [TestCase("background.flash")]
        [TestCase("background.image")]
        [TestCase("background.image.clear")]
        [TestCase("camera.shake")]
        [TestCase("sound.se")]
        [TestCase("sound.bgm")]
        [TestCase("sound.bgm.stop")]
        public void Create_ReturnsRoutine_ForStageCommands(string command)
        {
            Assert.IsNotNull(_stage.Create(command, null));
        }

        [Test]
        public void BackgroundImage_UsesTheRegisteredSprite_AndShowsIt()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                string asked = null;
                var stage = new DialogueStageCommands(
                    _backdrop,
                    findBackground: key =>
                    {
                        asked = key;
                        return sprite;
                    }
                );

                Drive(stage.Create("background.image", "lab,0"));

                Assert.AreEqual("lab", asked);
                var picture = Find("BackdropPicture");
                Assert.IsNotNull(picture);
                Assert.AreSame(sprite, picture.sprite);
                Assert.IsTrue(picture.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void BackgroundImage_WithUnregisteredKey_WarnsAndShowsNothing()
        {
            var stage = new DialogueStageCommands(_backdrop, findBackground: _ => null);
            LogAssert.Expect(LogType.Warning, new Regex("登録されていません"));

            Drive(stage.Create("background.image", "missing"));

            Assert.IsNull(Find("BackdropPicture"));
        }

        [Test]
        public void Sound_WithUnregisteredKey_WarnsAndDoesNotThrow()
        {
            var stage = new DialogueStageCommands(_backdrop, findSound: _ => null);
            LogAssert.Expect(LogType.Warning, new Regex("登録されていません"));

            Assert.DoesNotThrow(() => Drive(stage.Create("sound.se", "missing")));
        }
    }
}
