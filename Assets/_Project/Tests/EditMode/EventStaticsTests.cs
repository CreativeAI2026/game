using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// Play 開始時の static 初期化の検証。Domain Reload を切った Editor で、前回の Play の状態
    /// (会話途中で止めた再生中フラグ・破棄済みの seam)が次の Play に持ち越されないこと。
    /// </summary>
    public class EventStaticsTests
    {
        private sealed class FakeDialogueView : IDialogueView
        {
            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                yield break;
            }

            public IEnumerator ShowChoice(
                IReadOnlyList<ChoiceOption> options,
                System.Action<string> onSelected
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

            public IEnumerator Close()
            {
                yield break;
            }
        }

        [TearDown]
        public void TearDown() => EventStatics.ResetForPlaySession();

        [Test]
        public void Reset_ClearsPlayingFlag_WithoutNotifying()
        {
            EventPlaybackService.SetPlaying(true); // 会話の途中で Play を止めた
            bool notified = false;
            EventPlaybackService.PlayingChanged += _ => notified = true;

            EventStatics.ResetForPlaySession();

            Assert.IsFalse(EventPlaybackService.IsPlaying);
            Assert.IsFalse(notified, "前回の Play の購読者(破棄済み)には通知しない");
        }

        [Test]
        public void Reset_DropsPreviousSubscribers()
        {
            bool called = false;
            EventPlaybackService.PlayingChanged += _ => called = true;

            EventStatics.ResetForPlaySession();
            EventPlaybackService.SetPlaying(true);

            Assert.IsFalse(called, "前回の Play の購読が残らない");
        }

        [Test]
        public void Reset_ClearsSeams()
        {
            DialogueViewService.Current = new FakeDialogueView();

            EventStatics.ResetForPlaySession();

            Assert.IsNull(DialogueViewService.Current);
            Assert.IsNull(EventPlayerService.Current);
            Assert.IsNull(ItemGiverService.Current);
            Assert.IsNull(WeaponGiverService.Current);
            Assert.IsNull(BattleRunnerService.Current);
        }
    }
}
