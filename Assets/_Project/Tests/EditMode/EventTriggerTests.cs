using System;
using System.Collections.Generic;
using System.Reflection;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベント発火ゲートの検証(条件を満たしても戦闘中は新規イベントを発火しない)。
    /// EditMode では Instance が立たないため、静的プロパティをリフレクションで差し込んで OnTriggerEnter を直接叩く。
    /// </summary>
    public class EventTriggerTests
    {
        private sealed class RecordingEventPlayer : IEventPlayer
        {
            public readonly List<string> Played = new();

            public void Play(EventDefinition ev, BattleSetup battle = default) => Played.Add(ev.Id);
        }

        private sealed class FakeItemGiver : IItemGiver
        {
            public readonly HashSet<string> Owned = new();

            public void Give(string itemKey) => Owned.Add(itemKey);

            public bool HasImportantItem(string itemKey) => Owned.Contains(itemKey);
        }

        private GameObject _pmGo;
        private GameObject _gmmGo;
        private GameObject _triggerGo;
        private GameObject _playerGo;
        private ProgressManager _pm;
        private GameModeManager _gmm;
        private EventTrigger _trigger;
        private Collider _playerCollider;
        private RecordingEventPlayer _player;

        /// <summary>Awake 未実行でも Instance が要る。private set の静的プロパティへ直接入れる。</summary>
        private static void SetInstance<T>(T value)
        {
            typeof(T)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { value });
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        [SetUp]
        public void SetUp()
        {
            _pmGo = new GameObject("PM");
            _pm = _pmGo.AddComponent<ProgressManager>();
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            SetInstance(_pm);
            SetInstance(_gmm);

            _player = new RecordingEventPlayer();
            EventPlayerService.Current = _player;
            ItemGiverService.Current = new FakeItemGiver();

            _triggerGo = new GameObject("Trigger", typeof(BoxCollider));
            _trigger = _triggerGo.AddComponent<EventTrigger>();

            _playerGo = new GameObject("Player", typeof(BoxCollider)) { tag = "Player" };
            _playerCollider = _playerGo.GetComponent<Collider>();
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_trigger, "OnDisable");
            EventPlayerService.Current = null;
            ItemGiverService.Current = null;
            EventPlaybackService.SetPlaying(false);
            SetInstance<ProgressManager>(null);
            SetInstance<GameModeManager>(null);
            UnityEngine.Object.DestroyImmediate(_playerGo);
            UnityEngine.Object.DestroyImmediate(_triggerGo);
            UnityEngine.Object.DestroyImmediate(_gmmGo);
            UnityEngine.Object.DestroyImmediate(_pmGo);
        }

        private void AssignEvent(EventDefinition ev) => SetPrivateField(_trigger, "_event", ev);

        private void EnterTrigger() =>
            _trigger
                .GetType()
                .GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_trigger, new object[] { _playerCollider });

        private void ExitTrigger() =>
            TestReflection.Invoke(_trigger, "OnTriggerExit", _playerCollider);

        // EditMode では OnEnable が走らないので、進行度・再生状態の購読を明示的に行う。
        private void Subscribe() => TestReflection.Invoke(_trigger, "OnEnable");

        // 条件の見直しは次フレーム(Update)で行うので、1フレーム進める代わりに直接叩く。
        private void NextFrame() => TestReflection.Invoke(_trigger, "Update");

        private static EventDefinition NextEvent() =>
            EventDefinition.Create("robot_arrives", EventCondition.Progress(1));

        private static EventDefinition FiringEvent() =>
            EventDefinition.Create("cave_encounter", EventCondition.Progress(0));

        [Test]
        public void OnTriggerEnter_FieldMode_ConditionsMet_Fires()
        {
            AssignEvent(FiringEvent());

            EnterTrigger();

            CollectionAssert.AreEqual(new[] { "cave_encounter" }, _player.Played);
        }

        [Test]
        public void OnTriggerEnter_BattleMode_DoesNotFire()
        {
            AssignEvent(FiringEvent());
            _gmm.EnterBattle();
            Assert.AreEqual(GameMode.Battle, _gmm.CurrentMode); // 前提

            EnterTrigger();

            CollectionAssert.IsEmpty(_player.Played, "Battle 中は新規イベントを発火しない");
        }

        [Test]
        public void OnTriggerEnter_AfterExitBattle_FiresAgain()
        {
            AssignEvent(FiringEvent());
            _gmm.EnterBattle();
            EnterTrigger();
            CollectionAssert.IsEmpty(_player.Played);

            _gmm.ExitBattle();
            EnterTrigger();

            CollectionAssert.AreEqual(new[] { "cave_encounter" }, _player.Played);
        }

        [Test]
        public void OnTriggerEnter_NonPlayerCollider_DoesNotFire()
        {
            AssignEvent(FiringEvent());
            var other = new GameObject("Enemy", typeof(BoxCollider));
            try
            {
                _trigger
                    .GetType()
                    .GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_trigger, new object[] { other.GetComponent<Collider>() });

                CollectionAssert.IsEmpty(_player.Played);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void OnTriggerEnter_DuringEventPlayback_DoesNotFire()
        {
            AssignEvent(FiringEvent());
            EventPlaybackService.SetPlaying(true); // 会話中にトリガーへ入り直した

            EnterTrigger();

            CollectionAssert.IsEmpty(_player.Played, "再生中は二重発火しない");
        }

        [Test]
        public void PlayerInside_PreviousEventEnds_NextEventFires()
        {
            // 同じ場所で続けて起きるイベント。前のイベントの終了で進行度が 1 になり、出入りし直さずに発火する。
            AssignEvent(NextEvent());
            Subscribe();
            EnterTrigger(); // 進行度 0 なのでまだ発火しない

            EventPlaybackService.SetPlaying(true); // 前のイベントを再生中
            _pm.AdvanceTo(1); // 前のイベントの終わりで進行度が進む(まだ再生中なので弾かれる)
            CollectionAssert.IsEmpty(_player.Played, "再生中は発火しない");

            EventPlaybackService.SetPlaying(false); // 前のイベントの再生が終わった
            CollectionAssert.IsEmpty(_player.Played, "終了通知の配信中には始めない");

            NextFrame();

            CollectionAssert.AreEqual(new[] { "robot_arrives" }, _player.Played);
        }

        [Test]
        public void NextEvent_StartsAfterEndNotificationIsDelivered()
        {
            // 他の購読者(HUD 等)に「終了」が最後に届いて表示が崩れないよう、通知を配り終えてから次を始める。
            AssignEvent(NextEvent());
            Subscribe();
            EnterTrigger();
            var received = new List<bool>();
            EventPlaybackService.SetPlaying(true);
            _pm.AdvanceTo(1);
            EventPlaybackService.PlayingChanged += received.Add;

            EventPlaybackService.SetPlaying(false);

            CollectionAssert.AreEqual(new[] { false }, received, "配信中に次の開始が割り込まない");
            EventPlaybackService.PlayingChanged -= received.Add;
        }

        [Test]
        public void PlayerInside_ProgressChangedOutsidePlayback_Fires()
        {
            AssignEvent(NextEvent());
            Subscribe();
            EnterTrigger();

            _pm.AdvanceTo(1);
            NextFrame();

            CollectionAssert.AreEqual(new[] { "robot_arrives" }, _player.Played);
        }

        [Test]
        public void PlayerLeft_ProgressChanged_DoesNotFire()
        {
            AssignEvent(NextEvent());
            Subscribe();
            EnterTrigger();
            ExitTrigger();

            _pm.AdvanceTo(1);
            NextFrame();

            CollectionAssert.IsEmpty(_player.Played, "外に出たあとは発火しない");
        }

        [Test]
        public void Reset_MakesColliderTrigger()
        {
            var col = _triggerGo.GetComponent<Collider>();
            col.isTrigger = false;

            TestReflection.Invoke(_trigger, "Reset"); // コンポーネント追加時に呼ばれる

            Assert.IsTrue(col.isTrigger, "付け忘れるとただの壁になって発火しない");
        }

        [Test]
        public void OnTriggerEnter_ConditionsNotMet_DoesNotFire()
        {
            AssignEvent(EventDefinition.Create("later_event", EventCondition.Progress(5)));

            EnterTrigger();

            CollectionAssert.IsEmpty(_player.Played, "progress が一致しないので発火しない");
        }

        [Test]
        public void OnTriggerEnter_HasItemCondition_UsesItemGiverService()
        {
            var giver = new FakeItemGiver();
            ItemGiverService.Current = giver;
            AssignEvent(
                EventDefinition.Create(
                    "locked_door",
                    EventCondition.Progress(0),
                    EventCondition.HasItem("mysterious_key")
                )
            );

            EnterTrigger();
            CollectionAssert.IsEmpty(_player.Played, "鍵を持っていないので発火しない");

            giver.Give("mysterious_key");
            EnterTrigger();

            CollectionAssert.AreEqual(new[] { "locked_door" }, _player.Played);
        }
    }
}
