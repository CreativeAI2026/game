using System.Collections.Generic;
using System.Reflection;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 武器タブの表示更新(初期選択の反映・未設定時のフォールバック)の検証。
    /// </summary>
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

        [Test]
        public void PlacedMenuTabs_SwitchPanelsAndWeaponPresentation()
        {
            var root = Track(new GameObject("CharacterPanel", typeof(RectTransform)));
            root.SetActive(false);

            Transform header = CreateChild(root.transform, "Header");
            Transform tabBar = CreateChild(header, "TabBar");
            MenuTabButton characterTab = CreateMenuTabButton(tabBar, "CharacterTab");
            MenuTabButton loadoutTab = CreateMenuTabButton(tabBar, "LoadoutTab");

            Transform contentArea = CreateChild(root.transform, "ContentArea");
            GameObject loadoutView = CreateChild(contentArea, "LoadoutView").gameObject;
            GameObject characterView = CreateChild(contentArea, "CharacterView").gameObject;
            Transform weaponBar = CreateChild(characterView.transform, "WeaponChangeBar");
            var weaponTabs = new List<MenuTabButton>();
            for (int i = 0; i < 4; i++)
                weaponTabs.Add(CreateMenuTabButton(weaponBar, $"WeaponTab{i}"));

            var viewTabGroup = tabBar.gameObject.AddComponent<MenuTabPanelGroup>();
            SetField(
                viewTabGroup,
                "_tabs",
                new List<MenuTabPanelBinding>
                {
                    new(characterTab, characterView),
                    new(loadoutTab, loadoutView),
                }
            );

            var weaponNameObject = new GameObject("WeaponName", typeof(RectTransform));
            weaponNameObject.transform.SetParent(characterView.transform, false);
            weaponNameObject.AddComponent<CanvasRenderer>();
            var textType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.IsNotNull(textType);
            var weaponName = weaponNameObject.AddComponent(textType);
            var weaponSelector = characterView.AddComponent<CharacterWeaponSelector>();
            SetField(weaponSelector, "_weaponName", weaponName);
            WeaponData blade = CreateWeaponData("カッターブレード");
            WeaponData bow = CreateWeaponData("イーゼルボウ");
            WeaponData scythe = CreateWeaponData("アンブレラサイス");
            WeaponData hammer = CreateWeaponData("消火器ハンマー");
            SetField(
                weaponSelector,
                "_weapons",
                new List<CharacterWeaponSelection>
                {
                    new(weaponTabs[0], blade),
                    new(weaponTabs[1], bow),
                    new(weaponTabs[2], scythe),
                    new(weaponTabs[3], hammer),
                }
            );

            root.SetActive(true);
            InvokePrivate(weaponSelector, "OnEnable");

            Assert.AreEqual(0, viewTabGroup.CurrentIndex);
            Assert.AreEqual(0, weaponSelector.CurrentIndex);
            Assert.IsTrue(characterView.activeSelf);
            Assert.IsFalse(loadoutView.activeSelf);
            Assert.IsTrue(characterTab.IsSelected);
            Assert.IsFalse(loadoutTab.IsSelected);
            Assert.IsTrue(weaponTabs[0].IsSelected);

            loadoutTab.Button.onClick.Invoke();

            Assert.AreEqual(1, viewTabGroup.CurrentIndex);
            Assert.IsFalse(characterView.activeSelf);
            Assert.IsTrue(loadoutView.activeSelf);
            Assert.IsFalse(characterTab.IsSelected);
            Assert.IsTrue(loadoutTab.IsSelected);

            characterTab.Button.onClick.Invoke();
            weaponTabs[2].Button.onClick.Invoke();

            Assert.IsTrue(characterView.activeSelf);
            Assert.AreEqual(2, weaponSelector.CurrentIndex);
            Assert.AreEqual("アンブレラサイス", GetText(weaponName));
            for (int i = 0; i < weaponTabs.Count; i++)
                Assert.AreEqual(i == 2, weaponTabs[i].IsSelected);
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

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static MenuTabButton CreateMenuTabButton(Transform parent, string name)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.SetActive(false);
            buttonObject.transform.SetParent(parent, false);

            var underline = new GameObject("Underline", typeof(RectTransform));
            underline.transform.SetParent(buttonObject.transform, false);

            var menuTabButton = buttonObject.AddComponent<MenuTabButton>();
            buttonObject.SetActive(true);
            return menuTabButton;
        }

        private static string GetText(Component text) =>
            (string)text.GetType().GetProperty("text").GetValue(text);

        private WeaponData CreateWeaponData(string itemName)
        {
            var data = Track(ScriptableObject.CreateInstance<WeaponData>());
            data.itemName = itemName;
            data.description = $"{itemName}の説明";
            return data;
        }

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
