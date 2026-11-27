using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベント発火ゲートの検証(条件を満たしても戦闘中は新規イベントを発火しない)。
    /// EditMode では Instance が立たないため、静的プロパティをリフレクションで差し込んで OnTriggerEnter を直接叩く。
    /// 発火したかは、イベントの最初の台詞(イベント ID)が会話UIに届いたかで見る。
    /// </summary>
    public class EventTriggerTests
    {
        // 台詞を呼ばれた時点で記録する(EditMode ではコルーチンが最初の yield から先へ進まないため)。
        private sealed class RecordingView : IDialogueView
        {
            public readonly List<string> Played = new();
            public bool HiddenImmediately;

            public IEnumerator ShowLine(string speaker, string portrait, string text)
            {
                Played.Add(text);
                return Done();
            }

            public IEnumerator ShowChoice(
                IReadOnlyList<ChoiceOption> options,
                Action<string> onSelected
            ) => Done();

            public IEnumerator ShowItemPicker(string prompt, Action<string> onPicked) => Done();

            public IEnumerator ShowItemGet(string itemKey, string message) => Done();

            public IEnumerator ShowWeaponGet(string weaponKey, string message) => Done();

            public IEnumerator RunCommand(string command, string argument) => Done();

            public IEnumerator Close() => Done();

            public void HideImmediate() => HiddenImmediately = true;

            private static IEnumerator Done()
            {
                yield break;
            }
        }

        private readonly TestResidents _residents = new();
        private GameObject _triggerGo;
        private GameObject _playerGo;
        private ProgressManager _pm;
        private GameModeManager _gmm;
        private InventoryManager _inventory;
        private UIManager _ui;
        private EventTrigger _trigger;
        private Collider _playerCollider;
        private RecordingView _player;

        [SetUp]
        public void SetUp()
        {
            _pm = _residents.Add<ProgressManager>();
            _gmm = _residents.Add<GameModeManager>();
            _inventory = _residents.Add<InventoryManager>();
            _ui = _residents.AddUIManager();

            _player = new RecordingView();
            TestReflection.SetField(_ui, "_dialogueView", _player);

            _triggerGo = new GameObject("Trigger", typeof(BoxCollider));
            _trigger = _triggerGo.AddComponent<EventTrigger>();

            _playerGo = new GameObject("Player", typeof(BoxCollider)) { tag = "Player" };
            _playerCollider = _playerGo.GetComponent<Collider>();
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_trigger, "OnDisable");
            _residents.Clear();
            UnityEngine.Object.DestroyImmediate(_playerGo);
            UnityEngine.Object.DestroyImmediate(_triggerGo);
        }

        private void AssignEvent(EventDefinition ev) =>
            TestReflection.SetField(_trigger, "_event", ev);

        private void EnterTrigger() =>
            TestReflection.Invoke(_trigger, "OnTriggerEnter", _playerCollider);

        private void ExitTrigger() =>
            TestReflection.Invoke(_trigger, "OnTriggerExit", _playerCollider);

        // EditMode では OnEnable が走らないので、進行度・再生状態の購読を明示的に行う。
        private void Subscribe() => TestReflection.Invoke(_trigger, "OnEnable");

        // 条件の見直しは次フレーム(Update)で行うので、1フレーム進める代わりに直接叩く。
        private void NextFrame() => TestReflection.Invoke(_trigger, "Update");

        // 再生中のイベントを終わらせる。EditMode ではコルーチンが先へ進まないので、打ち切りで代える(フィールドへ戻る)。
        private void FinishPlayback() =>
            TestReflection.GetField<EventPlayer>(_trigger, "_player").Abort();

        // ID を台詞にして1行だけ話すイベント。進行度は進めない(発火の有無だけを見る)。
        private static EventDefinition TalkEvent(string id, params EventCondition[] conditions) =>
            EventDefinition.Create(
                id,
                conditions,
                new[] { EventStep.Line(null, null, id) },
                0,
                EventRepeat.Always,
                advancesProgress: false
            );

        private static EventDefinition NextEvent() =>
            TalkEvent("robot_arrives", EventCondition.Progress(1));

        private static EventDefinition FiringEvent() =>
            TalkEvent("cave_encounter", EventCondition.Progress(0));

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
            _gmm.TryStartBattle();
            Assert.AreEqual(GameMode.Battle, _gmm.Mode); // 前提

            EnterTrigger();

            CollectionAssert.IsEmpty(_player.Played, "Battle 中は新規イベントを発火しない");
        }

        [Test]
        public void OnTriggerEnter_AfterBattle_FiresAgain()
        {
            AssignEvent(FiringEvent());
            _gmm.TryStartBattle();
            EnterTrigger();
            CollectionAssert.IsEmpty(_player.Played);

            _gmm.ResetToField();
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
                TestReflection.Invoke(_trigger, "OnTriggerEnter", other.GetComponent<Collider>());

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
            _gmm.TryStartDialogue(); // 会話中にトリガーへ入り直した

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

            _gmm.TryStartDialogue(); // 前のイベントを再生中
            _pm.AdvanceTo(1); // 前のイベントの終わりで進行度が進む(まだ再生中なので弾かれる)
            CollectionAssert.IsEmpty(_player.Played, "再生中は発火しない");

            _gmm.ResetToField(); // 前のイベントの再生が終わった
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
            var received = new List<GameMode>();
            _gmm.TryStartDialogue();
            _pm.AdvanceTo(1);
            void OnChanged() => received.Add(_gmm.Mode);
            _gmm.Changed += OnChanged;

            _gmm.ResetToField();

            CollectionAssert.AreEqual(
                new[] { GameMode.Field },
                received,
                "配信中に次の開始が割り込まない"
            );
            _gmm.Changed -= OnChanged;
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
        public void PlayerInside_FlagChangedOutsidePlayback_Fires()
        {
            AssignEvent(
                TalkEvent(
                    "craft_done",
                    EventCondition.Progress(0),
                    EventCondition.Flag("crafted", "1")
                )
            );
            Subscribe();
            EnterTrigger();
            CollectionAssert.IsEmpty(_player.Played, "フラグが立つまでは発火しない");

            _pm.SetFlag("crafted", "1");
            NextFrame();

            CollectionAssert.AreEqual(new[] { "craft_done" }, _player.Played);
        }

        [Test]
        public void FlagSetWhileMenuOpen_FiresAfterReturningToField()
        {
            // 合成画面(Menu)を開いたまま調合してフラグが立っても、閉じて Field に戻るまでは始めない。
            AssignEvent(
                TalkEvent(
                    "craft_done",
                    EventCondition.Progress(0),
                    EventCondition.Flag("crafted", "1")
                )
            );
            Subscribe();
            EnterTrigger();
            _gmm.TryOpenMenu();

            _pm.SetFlag("crafted", "1");
            NextFrame();
            CollectionAssert.IsEmpty(_player.Played, "メニュー中は始めない");

            _gmm.ResetToField();
            NextFrame();

            CollectionAssert.AreEqual(new[] { "craft_done" }, _player.Played);
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
        public void PlayerMovedOutWithoutExitCallback_EventEnds_DoesNotFireAgain()
        {
            // 引き戻し(player.moveTo)は CharacterController を切って動かすので OnTriggerExit が届かない。
            // 外へ出ているのに「中に居る」と覚えたままだと、終わるたびに撃ち直して引き戻しが止まらない。
            AssignEvent(FiringEvent());
            Subscribe();
            EnterTrigger();
            CollectionAssert.AreEqual(new[] { "cave_encounter" }, _player.Played);

            _playerGo.transform.position = new Vector3(100f, 0f, 0f); // ExitTrigger は呼ばない
            FinishPlayback();
            NextFrame();

            Assert.AreEqual(1, _player.Played.Count, "外に居るので撃ち直さない");
        }

        [Test]
        public void PlayerStaysInside_AfterEventEnds_FiresAgain()
        {
            // 何度でも起きるイベントは、終わったあとも中に居れば撃ち直す(上のテストが撃ち直さない理由が位置だけであることの確認)。
            AssignEvent(FiringEvent());
            Subscribe();
            EnterTrigger();

            FinishPlayback();
            NextFrame();

            CollectionAssert.AreEqual(new[] { "cave_encounter", "cave_encounter" }, _player.Played);
        }

        [Test]
        public void Fire_SwitchesToDialogue()
        {
            AssignEvent(FiringEvent());

            EnterTrigger();

            Assert.AreEqual(GameMode.Dialogue, _gmm.Mode, "再生中は会話モード");
        }

        [Test]
        public void Disable_WhilePlaying_AbortsWithoutAdvancing()
        {
            // セーブ再開でシーンを読み直すと、トリガーごと消えて再生が打ち切られる。
            AssignEvent(
                EventDefinition.Create(
                    "cave_encounter",
                    new[] { EventCondition.Progress(0) },
                    new[] { EventStep.Line(null, null, "cave_encounter") },
                    1
                )
            );
            EnterTrigger();

            TestReflection.Invoke(_trigger, "OnDisable");

            Assert.AreEqual(GameMode.Field, _gmm.Mode, "フィールドへ戻す");
            Assert.IsTrue(_player.HiddenImmediately, "会話ウィンドウはその場で消す");
            Assert.AreEqual(0, _pm.Progress, "進行度は進めない");
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
            AssignEvent(TalkEvent("later_event", EventCondition.Progress(5)));

            EnterTrigger();

            CollectionAssert.IsEmpty(_player.Played, "progress が一致しないので発火しない");
        }

        [Test]
        public void OnTriggerEnter_HasItemCondition_UsesInventory()
        {
            var keyItem = ScriptableObject.CreateInstance<ItemData>();
            keyItem.id = 900;
            keyItem.key = "mysterious_key";
            keyItem.category = ItemCategory.Important;
            ItemCatalog.InjectForTests(new[] { keyItem });
            AssignEvent(
                TalkEvent(
                    "locked_door",
                    EventCondition.Progress(0),
                    EventCondition.HasItem("mysterious_key")
                )
            );

            EnterTrigger();
            CollectionAssert.IsEmpty(_player.Played, "鍵を持っていないので発火しない");

            _inventory.AddItem(keyItem, 1);
            EnterTrigger();

            CollectionAssert.AreEqual(new[] { "locked_door" }, _player.Played);
        }

        // --- マップピン ---

        [Test]
        public void Pin_Off_NeverShows()
        {
            AssignEvent(FiringEvent());

            _trigger.RefreshPin();

            Assert.IsFalse(_trigger.PinVisible);
        }

        [Test]
        public void Pin_On_ShowsWhileConditionsMet_AtTheTriggerCenter()
        {
            AssignEvent(FiringEvent());
            TestReflection.SetField(_trigger, "_showPin", true);
            _triggerGo.transform.position = new Vector3(3f, 2f, 5f);
            Physics.SyncTransforms(); // 動かした位置を Collider の bounds に反映させる

            _trigger.RefreshPin();

            Assert.IsTrue(_trigger.PinVisible);
            var pin = UnityEngine.Object.FindAnyObjectByType<MapPin>();
            Assert.IsNotNull(pin);
            Assert.That(pin.transform.position, Is.EqualTo(new Vector3(3f, 2f, 5f)));
        }

        [Test]
        public void Pin_On_ConditionsNotMet_DoesNotShow()
        {
            AssignEvent(EventDefinition.Create("later_event", EventCondition.Progress(5)));
            TestReflection.SetField(_trigger, "_showPin", true);

            _trigger.RefreshPin();

            Assert.IsFalse(_trigger.PinVisible);
        }

        [Test]
        public void Pin_UsesThePinPointWhenGiven()
        {
            AssignEvent(FiringEvent());
            TestReflection.SetField(_trigger, "_showPin", true);
            var point = new GameObject("PinPoint");
            point.transform.position = new Vector3(10f, 1f, -4f);
            TestReflection.SetField(_trigger, "_pinPoint", point.transform);

            _trigger.RefreshPin();

            var pin = UnityEngine.Object.FindAnyObjectByType<MapPin>();
            Assert.That(pin.transform.position, Is.EqualTo(new Vector3(10f, 1f, -4f)));
            UnityEngine.Object.DestroyImmediate(point);
        }

        [Test]
        public void Pin_DisappearsWhenTheEventFires()
        {
            AssignEvent(FiringEvent());
            TestReflection.SetField(_trigger, "_showPin", true);
            _trigger.RefreshPin();
            Assert.IsTrue(_trigger.PinVisible); // 前提

            EnterTrigger();

            Assert.IsFalse(_trigger.PinVisible);
            CollectionAssert.AreEqual(new[] { "cave_encounter" }, _player.Played);
        }

        [Test]
        public void Pin_DisappearsOnceProgressMovesOn()
        {
            AssignEvent(FiringEvent());
            TestReflection.SetField(_trigger, "_showPin", true);
            _trigger.RefreshPin();

            _pm.AdvanceTo(1);
            _trigger.RefreshPin();

            Assert.IsFalse(_trigger.PinVisible);
        }

        [Test]
        public void Pin_HidesWhileAnEventPlaysOrAMenuIsOpen()
        {
            AssignEvent(FiringEvent());
            TestReflection.SetField(_trigger, "_showPin", true);
            _trigger.RefreshPin();
            var pin = UnityEngine.Object.FindAnyObjectByType<MapPin>();
            var face = pin.transform.Find("Face").gameObject;
            TestReflection.Invoke(pin, "Update");
            Assert.IsTrue(pin.Shown);
            Assert.IsTrue(face.activeSelf);

            _gmm.TryStartDialogue();
            TestReflection.Invoke(pin, "Update");
            Assert.IsFalse(pin.Shown, "会話中は隠す");

            _gmm.ResetToField();
            _gmm.TryOpenMenu();
            TestReflection.Invoke(pin, "Update");
            Assert.IsFalse(pin.Shown, "メニューを開いている間は隠す");

            _gmm.ResetToField();
            TestReflection.Invoke(pin, "Update");
            Assert.IsTrue(pin.Shown);
            Assert.IsTrue(face.activeSelf);
        }
    }
}
