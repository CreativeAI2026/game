using System.Collections.Generic;
using System.Reflection;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// リボルバータブの選択状態(構築・移動・端でのラップ・決定)の検証。
    /// </summary>
    public class RevolverTabGroupStateTests
    {
        private GameObject _groupObject;
        private GameObject _rootObject;
        private GameObject _prefabObject;
        private RevolverTabGroup _group;
        private readonly List<TabDefinition> _definitions = new();

        [SetUp]
        public void SetUp()
        {
            _groupObject = new GameObject("RevolverTabGroup", typeof(RectTransform));
            _rootObject = new GameObject("ItemRoot", typeof(RectTransform));
            _rootObject.transform.SetParent(_groupObject.transform, false);
            _prefabObject = new GameObject(
                "ItemPrefab",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(RevolverTabItemView)
            );
            var tabButtonObject = new GameObject(
                "TabButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(TabButton)
            );
            tabButtonObject.transform.SetParent(_prefabObject.transform, false);
            tabButtonObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(tabButtonObject.transform, false);
            iconObject.GetComponent<Image>().raycastTarget = false;
            SetPrivateField(
                tabButtonObject.GetComponent<TabButton>(),
                "_icon",
                iconObject.GetComponent<Image>()
            );
            SetPrivateField(
                _prefabObject.GetComponent<RevolverTabItemView>(),
                "_tabButton",
                tabButtonObject.GetComponent<TabButton>()
            );
            _group = _groupObject.AddComponent<RevolverTabGroup>();

            var entries = new List<RevolverTabEntry>();
            for (int i = 0; i < 4; i++)
            {
                var definition = ScriptableObject.CreateInstance<TabDefinition>();
                definition.name = $"Tab {i}";
                _definitions.Add(definition);
                entries.Add(new RevolverTabEntry(definition));
            }

            SetField("_entries", entries);
            SetField("_itemPrefab", _prefabObject);
            SetField("_itemRoot", (RectTransform)_rootObject.transform);
            SetField("_initialIndex", 1);
            SetField("_moveDuration", 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_groupObject);
            Object.DestroyImmediate(_prefabObject);
            foreach (var definition in _definitions)
                Object.DestroyImmediate(definition);
            _definitions.Clear();
        }

        [Test]
        public void Build_CreatesOneItemPerEntryWithoutDuplicates()
        {
            Assert.IsTrue(_group.Build());
            Assert.AreEqual(4, _group.ItemCount);

            Assert.IsTrue(_group.Build());
            Assert.AreEqual(4, _group.ItemCount);
            Assert.AreEqual(4, _rootObject.transform.childCount);
        }

        [Test]
        public void Build_SelectsConfiguredInitialIndex()
        {
            _group.Build();

            Assert.AreEqual(1, _group.SelectedIndex);
            Assert.AreSame(_definitions[1], _group.CurrentDefinition);
        }

        [Test]
        public void Build_FiresInitialSelectionOnceAndFocusesSelectedItem()
        {
            int eventCount = 0;
            _group.SelectionChanged += (_, _, _) => eventCount++;
            var eventSystemObject = CreateActiveEventSystem();
            try
            {
                _group.Build();

                Assert.AreEqual(1, eventCount);
                var selected = EventSystem.current.currentSelectedGameObject;
                Assert.IsNotNull(selected);
                Assert.AreEqual(1, selected.GetComponent<RevolverTabItemView>().DataIndex);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void FocusedItem_ForwardsMoveToGroupWithoutSelectableNavigation()
        {
            var eventSystemObject = CreateActiveEventSystem();
            try
            {
                _group.Build();
                var selected = EventSystem.current.currentSelectedGameObject;
                var item = selected.GetComponent<RevolverTabItemView>();
                Assert.AreEqual(Navigation.Mode.None, item.Button.navigation.mode);
                var move = CreateMoveEvent(EventSystem.current, MoveDirection.Right);

                ExecuteEvents.Execute(selected, move, ExecuteEvents.moveHandler);

                Assert.AreEqual(2, _group.SelectedIndex);
                Assert.IsTrue(move.used);
                Assert.AreEqual(
                    2,
                    EventSystem
                        .current.currentSelectedGameObject.GetComponent<RevolverTabItemView>()
                        .DataIndex
                );
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void FocusedItem_InvalidAxisDoesNotConsumeOrChangeSelection()
        {
            var eventSystemObject = CreateActiveEventSystem();
            try
            {
                _group.Build();
                var selected = EventSystem.current.currentSelectedGameObject;
                var move = CreateMoveEvent(EventSystem.current, MoveDirection.Up);

                ExecuteEvents.Execute(selected, move, ExecuteEvents.moveHandler);

                Assert.AreEqual(1, _group.SelectedIndex);
                Assert.IsFalse(move.used);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void ImmediateSelect_ChangesSelectionAndFiresOnce()
        {
            _group.Build();
            int eventCount = 0;
            _group.SelectionChanged += (_, _, _) => eventCount++;

            _group.Select(3, true);
            _group.Select(3, true);

            Assert.AreEqual(3, _group.SelectedIndex);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void NextAndPrevious_WrapAtEnds()
        {
            _group.Build();

            _group.Select(3, true);
            _group.SelectNext();
            Assert.AreEqual(0, _group.SelectedIndex);

            _group.SelectPrevious();
            Assert.AreEqual(3, _group.SelectedIndex);
        }

        [Test]
        public void SubmitSelected_FiresCurrentEntry()
        {
            _group.Build();
            int submittedIndex = -1;
            _group.Submitted += (index, _, _) => submittedIndex = index;

            _group.SubmitSelected();

            Assert.AreEqual(1, submittedIndex);
        }

        [Test]
        public void MoveInput_UsesVerticalAxisForSidePlacements()
        {
            var layout = new RevolverTabLayoutSettings { Placement = RevolverArcPlacement.Left };
            SetField("_layout", layout);
            _group.Build();
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                _group.OnMove(
                    CreateMoveEvent(
                        eventSystemObject.GetComponent<EventSystem>(),
                        MoveDirection.Down
                    )
                );
                Assert.AreEqual(2, _group.SelectedIndex);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MoveInput_ReverseOrderStillMovesInPressedScreenDirection()
        {
            SetField(
                "_layout",
                new RevolverTabLayoutSettings
                {
                    Placement = RevolverArcPlacement.Top,
                    ReverseOrder = true,
                }
            );
            _group.Build();
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var move = CreateMoveEvent(
                    eventSystemObject.GetComponent<EventSystem>(),
                    MoveDirection.Right
                );
                _group.OnMove(move);

                Assert.AreEqual(0, _group.SelectedIndex);
                Assert.IsTrue(move.used);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MoveInput_InvalidAxisIsNotConsumed()
        {
            _group.Build();
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var move = CreateMoveEvent(
                    eventSystemObject.GetComponent<EventSystem>(),
                    MoveDirection.Up
                );
                _group.OnMove(move);

                Assert.AreEqual(1, _group.SelectedIndex);
                Assert.IsFalse(move.used);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MoveInput_DuringTweenIsNotConsumed()
        {
            SetField("_moveDuration", 1f);
            _group.Build();
            _group.SelectNext();
            Assert.IsTrue(_group.IsAnimating);
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var move = CreateMoveEvent(
                    eventSystemObject.GetComponent<EventSystem>(),
                    MoveDirection.Right
                );
                _group.OnMove(move);

                Assert.IsFalse(move.used);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MoveInput_CountZeroOrOneDoesNotThrow()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            var eventSystem = eventSystemObject.GetComponent<EventSystem>();
            SetField("_entries", new List<RevolverTabEntry>());
            Assert.DoesNotThrow(() =>
                _group.OnMove(CreateMoveEvent(eventSystem, MoveDirection.Right))
            );

            SetField("_entries", new List<RevolverTabEntry> { new(_definitions[0]) });
            Assert.IsTrue(_group.Build());
            Assert.DoesNotThrow(() =>
                _group.OnMove(CreateMoveEvent(eventSystem, MoveDirection.Right))
            );
            Assert.AreEqual(0, _group.SelectedIndex);
            Object.DestroyImmediate(eventSystemObject);
        }

        [Test]
        public void ItemBindAndUnbind_DoNotLeaveDuplicateButtonCallbacks()
        {
            var item = _prefabObject.GetComponent<RevolverTabItemView>();
            int firstCount = 0;
            int secondCount = 0;
            item.Bind(_definitions[0], 0, _ => firstCount++);
            item.Bind(_definitions[1], 1, _ => secondCount++);

            item.Button.onClick.Invoke();
            item.Unbind();
            item.Button.onClick.Invoke();

            Assert.AreEqual(0, firstCount);
            Assert.AreEqual(1, secondCount);
        }

        [Test]
        public void ItemBind_UsesVisibleGraphicInsteadOfTransparentButtonTargetForRaycasts()
        {
            var item = _prefabObject.GetComponent<RevolverTabItemView>();
            var target = item.Button.targetGraphic;
            var icon = item.Button.transform.Find("Icon").GetComponent<Image>();

            item.Bind(_definitions[0], 0, _ => { });

            Assert.IsFalse(target.raycastTarget);
            Assert.IsTrue(icon.raycastTarget);
        }

        [Test]
        public void ApplyLayout_AlphaZeroDisablesInteractionAndRaycasts()
        {
            var item = _prefabObject.GetComponent<RevolverTabItemView>();
            item.Bind(_definitions[0], 0, _ => { });

            item.ApplyLayout(new RevolverTabLayout(Vector2.zero, 1f, 0f, true, true, 0f), true);

            Assert.IsFalse(item.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(item.CanvasGroup.interactable);
            Assert.IsFalse(item.Button.interactable);
        }

        [Test]
        public void ApplyLayout_OutsideVisibleRangeDisablesInteractionAndRaycasts()
        {
            var item = _prefabObject.GetComponent<RevolverTabItemView>();
            item.Bind(_definitions[0], 0, _ => { });

            item.ApplyLayout(new RevolverTabLayout(Vector2.zero, 1f, 0.5f, false, false, 3f), true);

            Assert.IsFalse(item.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(item.CanvasGroup.interactable);
            Assert.IsFalse(item.Button.interactable);
        }

        [Test]
        public void ApplyLayout_VisibleItemRemainsClickable()
        {
            var item = _prefabObject.GetComponent<RevolverTabItemView>();
            item.Bind(_definitions[0], 0, _ => { });

            item.ApplyLayout(new RevolverTabLayout(Vector2.zero, 1f, 0.5f, true, true, 0f), true);

            Assert.IsTrue(item.CanvasGroup.blocksRaycasts);
            Assert.IsTrue(item.CanvasGroup.interactable);
            Assert.IsTrue(item.Button.interactable);
        }

        [Test]
        public void RefreshLayout_DuringMoveFadesOutgoingAndIncomingItemsContinuously()
        {
            SetField("_moveDuration", 1f);
            _group.Build();
            _group.SelectNext();

            var outgoing = GetItem(0);
            var incoming = GetItem(3);
            float outgoingStart = outgoing.CanvasGroup.alpha;
            float incomingStart = incoming.CanvasGroup.alpha;

            SetField("_selectionPosition", 1.5f);
            InvokeRefreshLayout();

            Assert.Less(outgoing.CanvasGroup.alpha, outgoingStart);
            Assert.Greater(incoming.CanvasGroup.alpha, incomingStart);
            Assert.Greater(outgoing.CanvasGroup.alpha, 0f);
            Assert.Greater(incoming.CanvasGroup.alpha, 0f);
            Assert.IsTrue(outgoing.gameObject.activeSelf);
            Assert.IsTrue(incoming.gameObject.activeSelf);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SmallCounts_RefreshAcrossFractionalSelectionWithoutExceptionOrActiveToggle(
            int count
        )
        {
            var entries = new List<RevolverTabEntry>();
            for (int i = 0; i < count; i++)
                entries.Add(new RevolverTabEntry(_definitions[i]));
            SetField("_entries", entries);
            SetField("_initialIndex", 0);
            Assert.IsTrue(_group.Build());

            Assert.DoesNotThrow(() =>
            {
                SetField("_selectionPosition", 0.49f);
                InvokeRefreshLayout();
                SetField("_selectionPosition", 0.51f);
                InvokeRefreshLayout();
            });

            for (int i = 0; i < count; i++)
                Assert.IsTrue(GetItem(i).gameObject.activeSelf);
        }

        [Test]
        public void RefreshLayout_FullyTransparentItemDoesNotBlockRaycasts()
        {
            _group.Build();
            var wrappedItem = GetItem(3);

            SetField("_selectionPosition", 1.001f);
            InvokeRefreshLayout();

            Assert.AreEqual(0f, wrappedItem.CanvasGroup.alpha, 0.0001f);
            Assert.IsFalse(wrappedItem.CanvasGroup.blocksRaycasts);
            Assert.IsFalse(wrappedItem.CanvasGroup.interactable);
        }

        [Test]
        public void StandaloneTabButtonBind_DoesNotChangeItsRaycastTarget()
        {
            var buttonObject = new GameObject(
                "StandaloneTabButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(TabButton)
            );
            try
            {
                var image = buttonObject.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;

                buttonObject.GetComponent<TabButton>().Bind(_definitions[0]);

                Assert.IsTrue(image.raycastTarget);
            }
            finally
            {
                Object.DestroyImmediate(buttonObject);
            }
        }

        private void SetField(string fieldName, object value)
        {
            SetPrivateField(_group, fieldName, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private RevolverTabItemView GetItem(int index)
        {
            foreach (Transform child in _rootObject.transform)
            {
                var item = child.GetComponent<RevolverTabItemView>();
                if (item != null && item.DataIndex == index)
                    return item;
            }

            Assert.Fail($"Item {index} was not found.");
            return null;
        }

        private void InvokeRefreshLayout()
        {
            var method = typeof(RevolverTabGroup).GetMethod(
                "RefreshLayout",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.IsNotNull(method);
            method.Invoke(_group, null);
        }

        private static AxisEventData CreateMoveEvent(
            EventSystem eventSystem,
            MoveDirection direction
        ) => new(eventSystem) { moveDir = direction };

        private static GameObject CreateActiveEventSystem()
        {
            var gameObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
            var eventSystem = gameObject.GetComponent<EventSystem>();
            var onEnable = typeof(EventSystem).GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            onEnable?.Invoke(eventSystem, null);
            EventSystem.current = eventSystem;
            return gameObject;
        }
    }
}
