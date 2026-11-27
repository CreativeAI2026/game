using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static CreativeAI.Tests.TestCoroutine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// events.json の useItem ステップ(アイテムを使う調べ方)の検証。
    /// </summary>
    public class EventUseItemTests
    {
        private const string Key = "card_key";

        private sealed class FakeView : IDialogueView
        {
            public readonly Queue<string> Picks = new(); // ピッカーが返す値(null = やめる)
            public readonly List<string> Lines = new();
            public readonly List<string> Prompts = new();
            public bool Closed;
            public bool HiddenImmediately;
            public bool HoldPick; // true の間はピッカーを開いたまま止めておく

            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                Lines.Add(text);
                yield break;
            }

            public IEnumerator ShowItemPicker(string prompt, Action<string> onPicked)
            {
                Prompts.Add(prompt);
                while (HoldPick)
                    yield return null;
                onPicked?.Invoke(Picks.Count > 0 ? Picks.Dequeue() : null);
                yield break;
            }

            public IEnumerator Close()
            {
                Closed = true;
                yield break;
            }

            public void HideImmediate() => HiddenImmediately = true;

            public IEnumerator ShowChoice(
                IReadOnlyList<ChoiceOption> options,
                Action<string> onSelected
            )
            {
                yield break;
            }

            public IEnumerator ShowItemGet(string itemKey, string message)
            {
                yield break;
            }

            public IEnumerator ShowWeaponGet(string weaponKey, string message)
            {
                yield break;
            }

            public IEnumerator RunCommand(string command, string argument)
            {
                yield break;
            }
        }

        private sealed class FakeItems : IItemGiver
        {
            public readonly Dictionary<string, int> Stock = new();

            public void Give(string itemKey) { }

            public bool HasImportantItem(string itemKey) => false;

            public int CountItem(string itemKey) =>
                Stock.TryGetValue(itemKey, out var count) ? count : 0;

            public bool Consume(string itemKey, int count)
            {
                if (CountItem(itemKey) < count)
                    return false;
                Stock[itemKey] -= count;
                return true;
            }
        }

        private sealed class MemoryProgress : IEventProgress
        {
            public readonly Dictionary<string, string> Flags = new();
            public int Progress;

            public string GetFlag(string key) =>
                Flags.TryGetValue(key, out var value) ? value : string.Empty;

            public void SetFlag(string key, string value) => Flags[key] = value;

            public void AdvanceTo(int progress) => Progress = progress;
        }

        private EventPlayer _player;
        private FakeView _view;
        private FakeItems _items;
        private MemoryProgress _progress;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeView();
            _items = new FakeItems();
            _progress = new MemoryProgress();
            _player = new EventPlayer(_progress, view: _view, items: _items);
        }

        // 「アイテムを使う」→ 正解なら「開いた」を言って進行度 1 へ進む、というイベント。
        private static EventDefinition UseItemEvent(
            bool consume = true,
            bool endOnWrong = false,
            int count = 1,
            EventRepeat repeat = EventRepeat.Progress,
            string usedText = null,
            string cancelText = null
        ) =>
            EventDefinition.Create(
                "use_item_test",
                null,
                new[]
                {
                    EventStep.UseItem(
                        new[] { new UseItemCandidate(Key, count) },
                        consume,
                        "違うようだ。",
                        endOnWrong: endOnWrong,
                        usedText: usedText,
                        cancelText: cancelText
                    ),
                    EventStep.Line("", "", "開いた"),
                },
                1,
                repeat
            );

        [Test]
        public void UseItem_Factory_ExposesFields()
        {
            var step = EventStep.UseItem(
                new[] { new UseItemCandidate("card_key", 2) },
                true,
                "違うようだ。",
                "係員",
                "p1",
                "何を使う?",
                true
            );

            Assert.AreEqual(StepKind.UseItem, step.Kind);
            Assert.AreEqual("card_key", step.Accept[0].ItemKey);
            Assert.AreEqual(2, step.Accept[0].Count);
            Assert.IsTrue(step.Consume);
            Assert.AreEqual("違うようだ。", step.Text);
            Assert.AreEqual("係員", step.Speaker);
            Assert.AreEqual("p1", step.Portrait);
            Assert.AreEqual("何を使う?", step.Prompt);
            Assert.IsTrue(step.EndOnWrong);
        }

        [Test]
        public void UseItem_Correct_Consume_ReducesAndContinues()
        {
            _items.Stock[Key] = 3;
            _view.Picks.Enqueue(Key);

            Drive(_player.Play(UseItemEvent(consume: true)));

            Assert.AreEqual(2, _items.Stock[Key], "正解の個数ぶん消費する");
            CollectionAssert.AreEqual(new[] { "開いた" }, _view.Lines);
            Assert.AreEqual(1, _progress.Progress, "最後まで流れて進行度が進む");
        }

        [Test]
        public void UseItem_Correct_NoConsume_KeepsItems()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(Key);

            Drive(_player.Play(UseItemEvent(consume: false)));

            Assert.AreEqual(1, _items.Stock[Key]);
            CollectionAssert.AreEqual(new[] { "開いた" }, _view.Lines);
        }

        [Test]
        public void UseItem_Wrong_Retry_ThenCorrect()
        {
            _items.Stock[Key] = 1;
            _items.Stock["apple"] = 1;
            _view.Picks.Enqueue("apple");
            _view.Picks.Enqueue(Key);

            Drive(_player.Play(UseItemEvent()));

            Assert.AreEqual(1, _items.Stock["apple"], "間違ったアイテムは消費しない");
            Assert.AreEqual(0, _items.Stock[Key]);
            CollectionAssert.AreEqual(new[] { "違うようだ。", "開いた" }, _view.Lines);
            Assert.AreEqual(2, _view.Prompts.Count, "間違えたらピッカーを開き直す");
            Assert.AreEqual(1, _progress.Progress);
        }

        [Test]
        public void UseItem_KeylessItem_IsWrong()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(string.Empty);
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent()));

            CollectionAssert.AreEqual(new[] { "違うようだ。" }, _view.Lines);
            Assert.AreEqual(1, _items.Stock[Key]);
        }

        [Test]
        public void UseItem_Cancel_AbortsWithoutProgress()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent()));

            Assert.AreEqual(0, _progress.Progress, "やめたら進行度を進めない");
            Assert.IsTrue(_view.Closed, "会話ウィンドウは閉じる");
            CollectionAssert.DoesNotContain(_view.Lines, "開いた");
            Assert.AreEqual(1, _items.Stock[Key]);
            Assert.IsFalse(_player.IsPlaying, "再生中フラグは戻す");
        }

        [Test]
        public void UseItem_Factory_ExposesUsedAndCancelText()
        {
            var step = EventStep.UseItem(
                new[] { new UseItemCandidate("card_key", 1) },
                true,
                "違うようだ。",
                usedText: "カードキーを使った。",
                cancelText: "やめておこう。"
            );

            Assert.AreEqual("カードキーを使った。", step.UsedText);
            Assert.AreEqual("やめておこう。", step.CancelText);
        }

        [Test]
        public void UseItem_Correct_WithUsedText_ShowsItBeforeContinuing()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(Key);

            Drive(_player.Play(UseItemEvent(usedText: "カードキーを使った。")));

            CollectionAssert.AreEqual(new[] { "カードキーを使った。", "開いた" }, _view.Lines);
        }

        [Test]
        public void UseItem_Cancel_WithCancelText_ShowsItThenAbortsWithoutProgress()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent(cancelText: "やめておこう。")));

            CollectionAssert.AreEqual(new[] { "やめておこう。" }, _view.Lines);
            Assert.AreEqual(0, _progress.Progress);
            Assert.IsTrue(_view.Closed);
        }

        [Test]
        public void UseItem_Cancel_AfterWrongAnswer_ShowsCancelText()
        {
            _items.Stock["apple"] = 1;
            _view.Picks.Enqueue("apple");
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent(cancelText: "やめておこう。")));

            CollectionAssert.AreEqual(new[] { "違うようだ。", "やめておこう。" }, _view.Lines);
        }

        [Test]
        public void UseItem_WrongWithEndOnWrong_AbortsWithoutProgress()
        {
            _items.Stock[Key] = 1;
            _items.Stock["apple"] = 1;
            _view.Picks.Enqueue("apple");
            _view.Picks.Enqueue(Key); // 打ち切られるので使われない

            Drive(_player.Play(UseItemEvent(endOnWrong: true)));

            CollectionAssert.AreEqual(new[] { "違うようだ。" }, _view.Lines);
            Assert.AreEqual(0, _progress.Progress);
            Assert.AreEqual(1, _items.Stock[Key], "打ち切りのあとに正解を使わない");
            Assert.IsTrue(_view.Closed);
        }

        [Test]
        public void UseItem_NotEnoughCount_IsWrongAndKeepsItems()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(Key); // 2個必要なのに1個しかない
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent(count: 2)));

            CollectionAssert.AreEqual(new[] { "違うようだ。" }, _view.Lines);
            Assert.AreEqual(1, _items.Stock[Key]);
        }

        [Test]
        public void UseItem_ExactCount_IsCorrect()
        {
            _items.Stock[Key] = 2;
            _view.Picks.Enqueue(Key);

            Drive(_player.Play(UseItemEvent(count: 2)));

            Assert.AreEqual(0, _items.Stock[Key]);
            CollectionAssert.AreEqual(new[] { "開いた" }, _view.Lines);
        }

        [Test]
        public void UseItem_AbortedWhilePicking_StopsWithoutProgress()
        {
            _items.Stock[Key] = 1;
            _view.Picks.Enqueue(Key);
            _view.HoldPick = true;
            DriveUntil(_player.Play(UseItemEvent()), () => _view.Prompts.Count > 0);

            LogAssert.Expect(LogType.Warning, new Regex("打ち切りました"));
            _player.Abort(); // コルーチンは発火側ごと止まるので、この先は回さない

            Assert.AreEqual(0, _progress.Progress);
            Assert.AreEqual(1, _items.Stock[Key], "打ち切ったら消費しない");
            CollectionAssert.DoesNotContain(_view.Lines, "開いた");
            Assert.IsTrue(_view.HiddenImmediately, "会話ウィンドウはその場で消す");
            Assert.IsFalse(_player.IsPlaying);
        }

        [Test]
        public void UseItem_AbortedOnceEvent_DoesNotSetSeen()
        {
            _view.Picks.Enqueue(null);

            Drive(_player.Play(UseItemEvent(repeat: EventRepeat.Once)));

            Assert.IsEmpty(_progress.GetFlag(ProgressFlags.Seen("use_item_test")));
        }
    }
}
