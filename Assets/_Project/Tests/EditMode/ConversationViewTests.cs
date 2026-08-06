using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using CreativeAI.UI.ConversationUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 会話UI(documents/Specification.md §5: 画面下に会話ウィンドウ、上半分に立ち絵)。
    /// 送り入力そのものは PlayMode の領分なので、ここでは
    /// 「話者名・本文・立ち絵キーの反映」と「選択肢が並ぶ」ところまでを検証する
    /// (選択後の後片付けは Destroy を使うので ConversationViewPlayModeTests 側)。
    /// </summary>
    public class ConversationViewTests
    {
        private GameObject _viewGo;
        private ConversationView _view;
        private TMP_Text _nameText;
        private TMP_Text _bodyText;
        private Image _portrait;
        private Button _choiceTemplate;
        private RectTransform _choiceContainer;
        private readonly List<UnityEngine.Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _viewGo = new GameObject("ConversationView", typeof(RectTransform));

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(_viewGo.transform);
            _nameText = nameGo.AddComponent<TextMeshProUGUI>();

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(_viewGo.transform);
            _bodyText = bodyGo.AddComponent<TextMeshProUGUI>();

            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            portraitGo.transform.SetParent(_viewGo.transform);
            _portrait = portraitGo.AddComponent<Image>();

            var containerGo = new GameObject("Choices", typeof(RectTransform));
            containerGo.transform.SetParent(_viewGo.transform);
            _choiceContainer = containerGo.GetComponent<RectTransform>();

            var templateGo = new GameObject("ChoiceButton", typeof(RectTransform));
            templateGo.transform.SetParent(containerGo.transform);
            templateGo.AddComponent<Image>();
            _choiceTemplate = templateGo.AddComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(templateGo.transform);
            labelGo.AddComponent<TextMeshProUGUI>();
            templateGo.SetActive(false);

            _view = _viewGo.AddComponent<ConversationView>();
            TestReflection.SetField(_view, "_nameText", _nameText);
            TestReflection.SetField(_view, "_bodyText", _bodyText);
            TestReflection.SetField(_view, "_portrait", _portrait);
            TestReflection.SetField(_view, "_choiceButtonTemplate", _choiceTemplate);
            TestReflection.SetField(_view, "_choiceContainer", _choiceContainer);
            TestReflection.SetField(_view, "_charInterval", 0f); // タイプ演出を待たない
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_viewGo);
            foreach (var a in _assets)
                UnityEngine.Object.DestroyImmediate(a);
            _assets.Clear();
        }

        /// <summary>入れ子の IEnumerator を展開しつつ、最大 maxSteps だけ進める(送り待ちで止まる想定)。</summary>
        private static void Drive(IEnumerator routine, int maxSteps = 500)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int i = 0; i < maxSteps && stack.Count > 0; i++)
            {
                var top = stack.Peek();
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator nested)
                        stack.Push(nested);
                }
                else
                {
                    stack.Pop();
                }
            }
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            _assets.Add(tex);
            _assets.Add(sprite);
            return sprite;
        }

        private void SetPortraitCatalog(params (string key, Sprite sprite)[] entries)
        {
            var list = new List<ConversationView.PortraitEntry>();
            foreach (var (key, sprite) in entries)
                list.Add(new ConversationView.PortraitEntry { Key = key, Sprite = sprite });
            TestReflection.SetField(_view, "_portraits", list.ToArray());
        }

        [Test]
        public void ShowLine_SetsSpeakerAndBody()
        {
            Drive(_view.ShowLine("主人公", null, "…誰だ?"));

            Assert.AreEqual("主人公", _nameText.text);
            Assert.AreEqual("…誰だ?", _bodyText.text);
        }

        [Test]
        public void ShowLine_NullSpeakerAndText_AreTreatedAsEmpty()
        {
            Drive(_view.ShowLine(null, null, null));

            Assert.AreEqual(string.Empty, _nameText.text);
            Assert.AreEqual(string.Empty, _bodyText.text);
        }

        [Test]
        public void ShowLine_KnownPortraitKey_AppliesThatSprite()
        {
            var hero = MakeSprite();
            var girl = MakeSprite();
            SetPortraitCatalog(("hero_normal", hero), ("girl_fear", girl));

            Drive(_view.ShowLine("はかなげ少女", "girl_fear", "来ないで……っ"));

            Assert.AreSame(girl, _portrait.sprite);
            Assert.IsTrue(_portrait.enabled, "立ち絵は表示される");
        }

        [Test]
        public void ShowLine_UnknownPortraitKey_HidesThePortraitInsteadOfThrowing()
        {
            SetPortraitCatalog(("hero_normal", MakeSprite()));

            Assert.DoesNotThrow(() => Drive(_view.ShowLine("主人公", "hero_smirk", "…")));

            Assert.IsFalse(_portrait.enabled, "カタログに無いキーは立ち絵なし扱い");
        }

        [Test]
        public void ShowLine_EmptyPortraitKey_HidesThePortrait()
        {
            SetPortraitCatalog(("hero_normal", MakeSprite()));

            Drive(_view.ShowLine("ナレーション", "", "……"));

            Assert.IsFalse(_portrait.enabled);
        }

        [Test]
        public void ShowChoice_SpawnsAButtonPerOption()
        {
            var options = new List<ChoiceOption>
            {
                new("一緒に行く", "together"),
                new("ひとりで行く", "alone"),
            };

            var routine = _view.ShowChoice(options, _ => { });
            routine.MoveNext(); // 選択肢を生成して待ちに入る

            var spawned = TestReflection.GetField<List<GameObject>>(_view, "_spawnedChoices");
            Assert.AreEqual(2, spawned.Count);
        }

        [Test]
        public void Awake_RegistersItselfToTheDialogueViewSeam()
        {
            // EventPlayer は DialogueViewService 経由で会話UIを見つける(spec §6 の seam)。
            var go = new GameObject("ConversationView2", typeof(RectTransform));
            try
            {
                var view = go.AddComponent<ConversationView>();
                TestReflection.Invoke(view, "Awake");

                Assert.AreSame(view, DialogueViewService.Current);

                TestReflection.Invoke(view, "OnDestroy");
                Assert.IsNull(DialogueViewService.Current, "破棄時に seam を解除する");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                DialogueViewService.Current = null;
            }
        }
    }
}
