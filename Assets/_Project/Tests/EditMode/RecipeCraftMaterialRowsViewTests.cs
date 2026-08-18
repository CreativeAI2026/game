using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.CraftingUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    public class RecipeCraftMaterialRowsViewTests
    {
        private GameObject _root;
        private ItemData _item;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_item);
        }

        [Test]
        public void ShowRows_WithoutAnimation_KeepsActiveRowOpacity()
        {
            _root = new GameObject("MaterialRows", typeof(RectTransform));
            var view = _root.AddComponent<RecipeCraftMaterialRowsView>();
            var rowObject = new GameObject("MaterialRow", typeof(CanvasGroup));
            rowObject.transform.SetParent(_root.transform, false);
            var row = rowObject.AddComponent<RecipeMaterialRow>();
            var canvasGroup = rowObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0.73f;
            TestReflection.SetField(view, "_rows", new List<RecipeMaterialRow> { row });

            _item = ScriptableObject.CreateInstance<ItemData>();
            var rows = new[] { new RecipeCraftMaterialRowData(_item, 2, 3) };

            view.ShowRows(rows, animate: false);

            Assert.IsTrue(rowObject.activeSelf);
            Assert.AreEqual(0.73f, canvasGroup.alpha, 0.001f);
        }
    }
}
