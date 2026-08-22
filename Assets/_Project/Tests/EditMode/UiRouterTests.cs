using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 操作で開く UI(キャラ / インベ / セーブ / 調合)の排他表示(documents/Specification.md §5)。
    /// 開くのは常に1つで、別のを開くと前のが閉じる。
    /// </summary>
    public class UiRouterTests
    {
        private GameObject _rootGo;
        private UiRouter _router;
        private GameObject _character;
        private GameObject _inventory;
        private GameObject _save;
        private GameObject _craft;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("UIRoot");
            _router = _rootGo.AddComponent<UiRouter>();

            _character = new GameObject("CharacterUI");
            _inventory = new GameObject("InventoryUI");
            _save = new GameObject("SaveUI");
            _craft = new GameObject("CraftUI");

            TestReflection.SetField(_router, "_characterUI", _character);
            TestReflection.SetField(_router, "_inventoryUI", _inventory);
            TestReflection.SetField(_router, "_saveUI", _save);
            TestReflection.SetField(_router, "_craftUI", _craft);

            // EditMode では Awake が走らないので、パネル登録を明示的に行う。
            TestReflection.Invoke(_router, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_craft);
            Object.DestroyImmediate(_save);
            Object.DestroyImmediate(_inventory);
            Object.DestroyImmediate(_character);
            Object.DestroyImmediate(_rootGo);
        }

        [Test]
        public void Awake_ClosesEverything()
        {
            Assert.IsFalse(_character.activeSelf);
            Assert.IsFalse(_inventory.activeSelf);
            Assert.IsFalse(_save.activeSelf);
            Assert.IsFalse(_craft.activeSelf);
            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Open_ShowsOnlyTheRequestedPanel()
        {
            _router.Open(UiRouter.UiId.Inventory);

            Assert.IsTrue(_inventory.activeSelf);
            Assert.IsFalse(_character.activeSelf);
            Assert.IsFalse(_save.activeSelf);
            Assert.IsFalse(_craft.activeSelf);
            Assert.IsTrue(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Open_Another_ClosesThePrevious()
        {
            _router.Open(UiRouter.UiId.Inventory);
            _router.Open(UiRouter.UiId.Save);

            Assert.IsFalse(_inventory.activeSelf, "排他: 前のパネルは閉じる");
            Assert.IsTrue(_save.activeSelf);
        }

        [Test]
        public void Open_None_ClosesAll()
        {
            _router.Open(UiRouter.UiId.Craft);
            _router.Open(UiRouter.UiId.None);

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void CloseAll_ClosesEveryPanel()
        {
            _router.Open(UiRouter.UiId.Character);

            _router.CloseAll();

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Toggle_OpensThenClosesTheSamePanel()
        {
            _router.Toggle(UiRouter.UiId.Character);
            Assert.IsTrue(_character.activeSelf);

            _router.Toggle(UiRouter.UiId.Character);
            Assert.IsFalse(_character.activeSelf, "同じものを押したら閉じる");
        }

        [Test]
        public void Toggle_SwitchesWhenAnotherPanelIsOpen()
        {
            _router.Open(UiRouter.UiId.Character);

            _router.Toggle(UiRouter.UiId.Inventory);

            Assert.IsFalse(_character.activeSelf);
            Assert.IsTrue(_inventory.activeSelf);
        }

        [Test]
        public void Toggle_AfterPanelClosedItself_ReopensIt()
        {
            // パネル側の戻るボタンで閉じた状態(activeSelf=false)からでも整合する。
            _router.Open(UiRouter.UiId.Save);
            _save.SetActive(false);

            _router.Toggle(UiRouter.UiId.Save);

            Assert.IsTrue(_save.activeSelf);
        }

        [Test]
        public void UnassignedPanel_IsIgnored()
        {
            // 調合UI 未割当(_craftUI = null)でも例外にならない。
            var go = new GameObject("Router2");
            try
            {
                var router = go.AddComponent<UiRouter>();
                TestReflection.SetField(router, "_characterUI", _character);
                TestReflection.Invoke(router, "Awake");

                Assert.DoesNotThrow(() => router.Open(UiRouter.UiId.Craft));
                Assert.IsFalse(router.IsAnyPanelOpen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
