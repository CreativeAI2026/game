using System.Collections.Generic;
using System.Reflection;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
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
            SetField("_itemPrefab", _prefabObject.GetComponent<RevolverTabItemView>());
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
    }
}
