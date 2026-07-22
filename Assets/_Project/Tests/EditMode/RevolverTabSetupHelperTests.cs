using System;
using System.Reflection;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    public class RevolverTabSetupHelperTests
    {
        private Type _helperType;

        [SetUp]
        public void SetUp()
        {
            _helperType = Type.GetType(
                "CreativeAI.EditorTools.UI.RevolverTabSetupHelper, CreativeAI.EditorTools"
            );
            Assert.IsNotNull(_helperType);
        }

        [Test]
        public void AutoAssignItem_AssignsOnlyUniqueTabButton()
        {
            var root = CreateItem("Item", 1, out var item);
            try
            {
                Assert.IsTrue(InvokeAutoAssign(item));
                Assert.IsNotNull(item.TabButton);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AutoAssignItem_LeavesAmbiguousCandidatesUnassigned()
        {
            var root = CreateItem("Item", 2, out var item);
            try
            {
                Assert.IsFalse(InvokeAutoAssign(item));
                Assert.IsNull(item.TabButton);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CharacterPanel_PreservesConfiguredLayoutValues()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab"
            );
            Assert.IsNotNull(prefab);
            var group = prefab.GetComponentInChildren<RevolverTabGroup>(true);
            Assert.IsNotNull(group);

            var serializedObject = new SerializedObject(group);
            var layout = serializedObject.FindProperty("_layout");
            Assert.AreEqual(180f, layout.FindPropertyRelative("_tangentRadius").floatValue);
            Assert.AreEqual(80f, layout.FindPropertyRelative("_arcDepth").floatValue);
            Assert.AreEqual(
                (int)RevolverArcPlacement.Top,
                layout.FindPropertyRelative("_placement").enumValueIndex
            );
        }

        [Test]
        public void ApplyPlacementToRoot_UsesPlacementAnchorAndSupportsUndo()
        {
            var root = new GameObject("Group", typeof(RectTransform), typeof(RevolverTabGroup));
            try
            {
                var group = root.GetComponent<RevolverTabGroup>();
                var serializedObject = new SerializedObject(group);
                serializedObject
                    .FindProperty("_layout")
                    .FindPropertyRelative("_placement")
                    .enumValueIndex = (int)RevolverArcPlacement.Left;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();

                var method = _helperType.GetMethod(
                    "ApplyPlacementToRoot",
                    BindingFlags.Public | BindingFlags.Static
                );
                Assert.IsTrue((bool)method.Invoke(null, new object[] { group }));
                Assert.AreEqual(new Vector2(0f, 0.5f), ((RectTransform)root.transform).anchorMin);
                Assert.AreEqual(new Vector2(0f, 0.5f), ((RectTransform)root.transform).pivot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private bool InvokeAutoAssign(RevolverTabItemView item)
        {
            var method = _helperType.GetMethod(
                "AutoAssign",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(RevolverTabItemView), typeof(bool) },
                null
            );
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { item, false });
        }

        private static GameObject CreateItem(
            string name,
            int tabButtonCount,
            out RevolverTabItemView item
        )
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(RevolverTabItemView)
            );
            item = root.GetComponent<RevolverTabItemView>();

            for (int i = 0; i < tabButtonCount; i++)
            {
                var button = new GameObject(
                    $"TabButton {i}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(TabButton)
                );
                button.transform.SetParent(root.transform, false);
            }
            return root;
        }
    }
}
