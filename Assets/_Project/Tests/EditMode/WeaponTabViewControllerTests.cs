using System.Collections.Generic;
using System.Reflection;
using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    public class WeaponTabViewControllerTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void RevolverInitialSelection_UpdatesDisplayOnceBuildCompletes()
        {
            var group = CreateRevolverGroup();
            var controller = CreateController(out var weaponName);
            SetField(controller, "_revolverTabGroup", group);
            SetText(weaponName, "pending");

            InvokePrivate(controller, "OnEnable");
            Assert.AreEqual("pending", GetText(weaponName));
            Assert.IsTrue(group.Build());

            Assert.AreNotEqual("pending", GetText(weaponName));
        }

        [Test]
        public void MissingRevolver_ShowsDefaultWeapon()
        {
            var controller = CreateController(out var weaponName);
            SetText(weaponName, "pending");

            InvokePrivate(controller, "OnEnable");

            Assert.AreNotEqual("pending", GetText(weaponName));
        }

        private RevolverTabGroup CreateRevolverGroup()
        {
            var groupObject = Track(new GameObject("Revolver", typeof(RectTransform)));
            var itemRoot = Track(new GameObject("ItemRoot", typeof(RectTransform)));
            itemRoot.transform.SetParent(groupObject.transform, false);
            var itemPrefab = Track(
                new GameObject(
                    "ItemPrefab",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(RevolverTabItemView)
                )
            );
            var button = new GameObject(
                "Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(TabButton)
            );
            button.transform.SetParent(itemPrefab.transform, false);
            SetField(
                itemPrefab.GetComponent<RevolverTabItemView>(),
                "_tabButton",
                button.GetComponent<TabButton>()
            );

            var definition = Track(ScriptableObject.CreateInstance<TabDefinition>());
            var group = groupObject.AddComponent<RevolverTabGroup>();
            SetField(group, "_entries", new List<RevolverTabEntry> { new(definition) });
            SetField(group, "_itemPrefab", itemPrefab);
            SetField(group, "_itemRoot", (RectTransform)itemRoot.transform);
            return group;
        }

        private WeaponTabViewController CreateController(out Component weaponName)
        {
            var controllerObject = Track(new GameObject("Controller"));
            controllerObject.SetActive(false);
            var textObject = new GameObject("WeaponName", typeof(RectTransform));
            textObject.transform.SetParent(controllerObject.transform, false);
            textObject.AddComponent<CanvasRenderer>();
            var textType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.IsNotNull(textType);
            weaponName = textObject.AddComponent(textType);
            var controller = controllerObject.AddComponent<WeaponTabViewController>();
            SetField(controller, "_weaponName", weaponName);
            return controller;
        }

        private static string GetText(Component text) =>
            (string)text.GetType().GetProperty("text").GetValue(text);

        private static void SetText(Component text, string value)
        {
            text.GetType().GetProperty("text").SetValue(text, value);
        }

        private T Track<T>(T value)
            where T : Object
        {
            _objects.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target
                .GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target
                .GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }
    }
}
