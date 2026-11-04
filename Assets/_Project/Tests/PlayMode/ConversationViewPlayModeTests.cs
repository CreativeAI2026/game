using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// 会話UIの選択肢の検証(提示 → 選択 → 後片付け)。
    /// 選び終わったあとの後片付けが Destroy を使うので、EditMode ではなくここで回す。
    /// </summary>
    public class ConversationViewPlayModeTests
    {
        private GameObject _viewGo;
        private ConversationView _view;
        private RectTransform _choiceContainer;

        [SetUp]
        public void SetUp()
        {
            _viewGo = new GameObject(
                "ConversationView",
                typeof(RectTransform),
                typeof(CanvasGroup)
            );
            _viewGo.SetActive(false);

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(_viewGo.transform);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(_viewGo.transform);
            var bodyText = bodyGo.AddComponent<TextMeshProUGUI>();

            var containerGo = new GameObject("Choices", typeof(RectTransform));
            containerGo.transform.SetParent(_viewGo.transform);
            _choiceContainer = containerGo.GetComponent<RectTransform>();

            var templateGo = new GameObject("ChoiceButton", typeof(RectTransform));
            templateGo.transform.SetParent(containerGo.transform);
            templateGo.AddComponent<Image>();
            var template = templateGo.AddComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(templateGo.transform);
            labelGo.AddComponent<TextMeshProUGUI>();
            templateGo.SetActive(false);

            _view = _viewGo.AddComponent<ConversationView>();
            TestReflection.SetField(_view, "_root", _viewGo.GetComponent<CanvasGroup>());
            TestReflection.SetField(_view, "_windowRoot", _viewGo.GetComponent<RectTransform>());
            TestReflection.SetField(_view, "_nameText", nameText);
            TestReflection.SetField(_view, "_bodyText", bodyText);
            TestReflection.SetField(_view, "_choiceButtonTemplate", template);
            TestReflection.SetField(_view, "_choiceContainer", _choiceContainer);
            TestReflection.SetField(_view, "_charInterval", 0f);
            _viewGo.SetActive(true);
            _viewGo.GetComponent<CanvasGroup>().alpha = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDownRoutine()
        {
            Object.Destroy(_viewGo);
            DialogueViewService.Current = null;
            // Destroy が効くまで待つ。残っていると次のテストの Awake が
            // 二重生成ガードで自滅して、選択肢が並ばなくなる。
            yield return null;
        }

        private List<GameObject> Spawned =>
            TestReflection.GetField<List<GameObject>>(
                TestReflection.GetField<object>(_view, "_choicePresenter"),
                "_spawned"
            );

        private void AdvanceUntilChoicesSpawned(NestedCoroutineDriver routine)
        {
            for (int i = 0; i < 10 && Spawned.Count == 0; i++)
                Assert.IsTrue(routine.MoveNext(), "選択肢が生成される前にコルーチンが終了した");
        }

        private sealed class NestedCoroutineDriver
        {
            private readonly Stack<IEnumerator> _stack = new();

            public NestedCoroutineDriver(IEnumerator routine) => _stack.Push(routine);

            public bool MoveNext()
            {
                while (_stack.Count > 0)
                {
                    var current = _stack.Peek();
                    if (!current.MoveNext())
                    {
                        _stack.Pop();
                        continue;
                    }

                    if (current.Current is IEnumerator nested)
                    {
                        _stack.Push(nested);
                        continue;
                    }

                    return true;
                }

                return false;
            }
        }

        private static List<ChoiceOption> TwoOptions() =>
            new()
            {
                new ChoiceOption("一緒に行く", "together"),
                new ChoiceOption("ひとりで行く", "alone"),
            };

        [UnityTest]
        public IEnumerator ShowChoice_ClickingAnOption_ReturnsItsValueAndCleansUp()
        {
            string picked = null;
            var routine = new NestedCoroutineDriver(
                _view.ShowChoice(TwoOptions(), v => picked = v)
            );
            AdvanceUntilChoicesSpawned(routine);
            Assert.AreEqual(2, Spawned.Count);

            Spawned[1].GetComponent<Button>().onClick.Invoke(); // 「ひとりで行く」
            while (routine.MoveNext())
                yield return null;

            Assert.AreEqual("alone", picked, "選んだ値が flag に書かれる値として返る");
            Assert.AreEqual(0, Spawned.Count, "選び終わったら選択肢は片付ける");
        }

        [UnityTest]
        public IEnumerator ShowChoice_FirstOption_ReturnsItsValue()
        {
            string picked = null;
            var routine = new NestedCoroutineDriver(
                _view.ShowChoice(TwoOptions(), v => picked = v)
            );
            AdvanceUntilChoicesSpawned(routine);

            Spawned[0].GetComponent<Button>().onClick.Invoke();
            while (routine.MoveNext())
                yield return null;

            Assert.AreEqual("together", picked);
        }

        [UnityTest]
        public IEnumerator ShowChoice_WaitsUntilSomethingIsPicked()
        {
            bool called = false;
            var routine = new NestedCoroutineDriver(
                _view.ShowChoice(TwoOptions(), _ => called = true)
            );

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(routine.MoveNext(), "選ぶまで進行は止まる");
                yield return null;
            }

            Assert.IsFalse(called, "未選択のうちはコールバックしない");
            Spawned[0].GetComponent<Button>().onClick.Invoke();
            while (routine.MoveNext())
                yield return null;
            Assert.IsTrue(called);
        }

        [UnityTest]
        public IEnumerator ShowChoice_AfterEntranceAnimation_ButtonsAreVisible()
        {
            string picked = null;
            _view.StartCoroutine(_view.ShowChoice(TwoOptions(), value => picked = value));

            yield return PlayModeWait.Until(
                () =>
                    Spawned.Count == 2
                    && Spawned.TrueForAll(c => c.GetComponent<CanvasGroup>().alpha > 0.99f),
                "選択肢の登場演出が終わる"
            );

            Spawned[0].GetComponent<Button>().onClick.Invoke();
            yield return PlayModeWait.Until(() => picked != null, "選んだ値が返る");
            Assert.AreEqual("together", picked);
        }
    }
}
