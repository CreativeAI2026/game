using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using CreativeAI.UI.ConversationUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
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

        private List<GameObject> SpawnedChoices
        {
            get
            {
                var presenter = TestReflection.GetField<object>(_view, "_choicePresenter");
                return presenter == null
                    ? null
                    : TestReflection.GetField<List<GameObject>>(presenter, "_spawned");
            }
        }

        private void DriveUntilChoicesSpawn(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int i = 0; i < 500 && stack.Count > 0; i++)
            {
                var top = stack.Peek();
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator nested)
                        stack.Push(nested);
                }
                else
                    stack.Pop();
                if (SpawnedChoices is { Count: > 0 })
                    break;
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

        private void SetPortraitCatalog(
            DialoguePortraitSide side = DialoguePortraitSide.Left,
            params (string key, Sprite sprite)[] entries
        )
        {
            var definition = ScriptableObject.CreateInstance<DialogueCharacterDefinition>();
            var expressions = new List<DialogueCharacterDefinition.Expression>();
            foreach (var (key, sprite) in entries)
                expressions.Add(
                    new DialogueCharacterDefinition.Expression
                    {
                        PortraitKey = key,
                        Sprite = sprite,
                        Icon = sprite,
                    }
                );
            TestReflection.SetField(definition, "_side", side);
            TestReflection.SetField(definition, "_expressions", expressions.ToArray());
            TestReflection.SetField(_view, "_characters", new[] { definition });
            _assets.Add(definition);
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
            SetPortraitCatalog(entries: new[] { ("hero_normal", hero), ("girl_fear", girl) });

            Drive(_view.ShowLine("はかなげ少女", "girl_fear", "来ないで……っ"));

            Assert.AreSame(girl, _portrait.sprite);
            Assert.IsTrue(_portrait.enabled, "立ち絵は表示される");
        }

        [Test]
        public void ShowLine_UnknownPortraitKey_HidesThePortraitInsteadOfThrowing()
        {
            SetPortraitCatalog(entries: new[] { ("hero_normal", MakeSprite()) });

            Assert.DoesNotThrow(() => Drive(_view.ShowLine("主人公", "hero_smirk", "…")));

            Assert.IsFalse(_portrait.enabled, "カタログに無いキーは立ち絵なし扱い");
        }

        [Test]
        public void ShowLine_EmptyPortraitKey_HidesThePortrait()
        {
            SetPortraitCatalog(entries: new[] { ("hero_normal", MakeSprite()) });

            Drive(_view.ShowLine("ナレーション", "", "……"));

            Assert.IsFalse(_portrait.enabled);
        }

        [Test]
        public void ShowLine_PlacesProtagonistLeftAndOtherCharactersRight()
        {
            var protagonist = MakeSprite();
            var otherCharacter = MakeSprite();
            SetPortraitCatalog(DialoguePortraitSide.Left, ("protagonist", protagonist));
            var protagonistDefinition = TestReflection.GetField<DialogueCharacterDefinition[]>(
                _view,
                "_characters"
            )[0];
            SetPortraitCatalog(DialoguePortraitSide.Right, ("other", otherCharacter));
            var otherDefinition = TestReflection.GetField<DialogueCharacterDefinition[]>(
                _view,
                "_characters"
            )[0];
            TestReflection.SetField(
                _view,
                "_characters",
                new[] { protagonistDefinition, otherDefinition }
            );

            Drive(_view.ShowLine("主人公", "protagonist", "左側"));
            Assert.AreEqual(0.12f, _portrait.rectTransform.anchorMin.x, 0.001f);
            Assert.AreEqual(0.12f, _portrait.rectTransform.anchorMax.x, 0.001f);
            Assert.AreEqual(1.03f, _portrait.rectTransform.localScale.x, 0.001f);

            Drive(_view.ShowLine("相手", "other", "右側"));
            var rightPortrait = TestReflection.GetField<Image>(_view, "_rightPortrait");
            Assert.AreEqual(0.88f, rightPortrait.rectTransform.anchorMin.x, 0.001f);
            Assert.AreEqual(0.88f, rightPortrait.rectTransform.anchorMax.x, 0.001f);
            Assert.AreEqual(-1.03f, rightPortrait.rectTransform.localScale.x, 0.001f);
            Assert.AreEqual(0.92f, _portrait.rectTransform.localScale.x, 0.001f);
            Assert.AreEqual(0.45f, _portrait.color.r, 0.001f);

            Drive(_view.ShowLine("主人公", "protagonist", "もう一度左側"));
            Assert.AreEqual(1.03f, _portrait.rectTransform.localScale.x, 0.001f);
            Assert.AreEqual(-0.92f, rightPortrait.rectTransform.localScale.x, 0.001f);
            Assert.AreEqual(0.45f, rightPortrait.color.r, 0.001f);
        }

        [Test]
        public void SetPortraitObscured_PersistsUntilReveal()
        {
            var sprite = MakeSprite();
            SetPortraitCatalog(entries: new[] { ("mystery", sprite) });
            Drive(_view.ShowLine("？？？", "mystery", "……"));

            Drive(_view.SetPortraitObscured(DialoguePortraitSide.Left, true, 0.01f));
            Assert.Less(_portrait.color.r, 0.1f);

            Drive(_view.SetPortraitObscured(DialoguePortraitSide.Left, false, 0.01f));
            Assert.Greater(_portrait.color.r, 0.9f);
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
            DriveUntilChoicesSpawn(routine);
            routine.MoveNext(); // 選択肢を生成して待ちに入る

            Assert.AreEqual(2, SpawnedChoices.Count);
            Assert.AreEqual(ConversationView.ConversationState.ShowingChoices, _view.State);
        }

        [Test]
        public void ShowChoice_NoValidOptions_CompletesSafely()
        {
            string picked = "not-called";
            var routine = _view.ShowChoice(Array.Empty<ChoiceOption>(), value => picked = value);

            LogAssert.Expect(LogType.Warning, "[ConversationView] 表示できる選択肢がありません。");
            Drive(routine);
            Assert.IsNull(picked);
            Assert.AreEqual(ConversationView.ConversationState.Entering, _view.State);
        }

        [TestCase(2, 178f, 124f)]
        [TestCase(3, 286f, 70f)]
        public void ShowChoice_AdjustsContainerAboveWindow(
            int choiceCount,
            float expectedHeight,
            float expectedBottom
        )
        {
            var options = new List<ChoiceOption>();
            for (int i = 0; i < choiceCount; i++)
                options.Add(new ChoiceOption($"選択肢{i + 1}", $"choice_{i + 1}"));

            var routine = _view.ShowChoice(options, _ => { });
            DriveUntilChoicesSpawn(routine);

            Assert.AreEqual(565f, _choiceContainer.rect.width, 0.01f);
            Assert.AreEqual(expectedHeight, _choiceContainer.rect.height, 0.01f);
            Assert.AreEqual(new Vector2(0.5f, 1f), _choiceContainer.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 1f), _choiceContainer.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, 0f), _choiceContainer.pivot);
            Assert.AreEqual(new Vector2(0f, expectedBottom), _choiceContainer.anchoredPosition);
        }

        [Test]
        public void NextIndicator_BouncesUpAndReturnsToBasePosition()
        {
            TestReflection.Invoke(_view, "InitializePresenters");
            var chrome = TestReflection.GetField<object>(_view, "_chromePresenter");
            Assert.AreEqual(
                0f,
                (float)TestReflection.Invoke(chrome, "CalculateIndicatorBounceOffset", 0f, 8f),
                0.001f
            );
            Assert.AreEqual(
                8f,
                (float)TestReflection.Invoke(chrome, "CalculateIndicatorBounceOffset", 0.5f, 8f),
                0.001f
            );
            Assert.AreEqual(
                0f,
                (float)TestReflection.Invoke(chrome, "CalculateIndicatorBounceOffset", 1f, 8f),
                0.001f
            );
        }

        [Test]
        public void SetAutoMode_TogglesModeWithoutAdvancingChoices()
        {
            Assert.IsFalse(_view.IsAutoMode);

            _view.SetAutoMode(true);
            Assert.IsTrue(_view.IsAutoMode);

            _view.SetAutoMode(false);
            Assert.IsFalse(_view.IsAutoMode);
        }

        [Test]
        public void ReadHistory_CanBeMarkedQueriedAndCleared()
        {
            _view.MarkLineRead("主人公", "protagonist_normal", "既読行");
            Assert.IsTrue(_view.IsLineRead("主人公", "protagonist_normal", "既読行"));

            _view.ClearReadHistory();
            Assert.IsFalse(_view.IsLineRead("主人公", "protagonist_normal", "既読行"));
        }

        [Test]
        public void SetTextSpeed_UpdatesConfiguredSpeed()
        {
            _view.SetTextSpeed(ConversationView.TextSpeed.Fast);

            Assert.AreEqual(
                ConversationView.TextSpeed.Fast,
                TestReflection.GetField<ConversationView.TextSpeed>(_view, "_textSpeed")
            );
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
