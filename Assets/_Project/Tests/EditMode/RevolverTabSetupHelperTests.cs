using System;
using System.Reflection;
using CreativeAI.UI;
using NUnit.Framework;
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
