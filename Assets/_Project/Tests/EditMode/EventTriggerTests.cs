using System;
using System.Collections.Generic;
using System.Reflection;
using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// EventTrigger の発火ゲートの検証。特に
    /// 「Battle 中は新規イベントを発火しない」(documents/Specification.md §6 の常駐アーキ図:
    /// EventTrigger →参照(モード:Battle中は発火しない)→ GameModeManager)。
    ///
    /// EditMode では Awake が走らず ProgressManager/GameModeManager の Instance が立たないため、
    /// 静的プロパティをリフレクションで差し込んでから OnTriggerEnter を直接叩く。
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
            EventPlayerService.Current = null;
            ItemGiverService.Current = null;
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
