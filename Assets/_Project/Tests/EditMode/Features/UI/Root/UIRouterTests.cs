using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 操作で開く UI(キャラ / インベ / セーブ / 調合)が1つずつしか開かないことの検証。
    /// 開くのは常に1つで、別のを開くと前のが閉じる。
    /// どれかが開いているかは UIPanelService に伝わる(プレイヤーの操作を止めるため)。
    /// </summary>
    public class UIRouterTests
    {
        private GameObject _rootGo;
        private UIRouter _router;
        private GameObject _character;
        private GameObject _inventory;
        private GameObject _save;
        private GameObject _craft;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("UIRoot");
            _router = _rootGo.AddComponent<UIRouter>();

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
            _router.Open(UIRouter.UIId.Inventory);

            Assert.IsTrue(_inventory.activeSelf);
            Assert.IsFalse(_character.activeSelf);
            Assert.IsFalse(_save.activeSelf);
            Assert.IsFalse(_craft.activeSelf);
            Assert.IsTrue(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Open_Another_ClosesThePrevious()
        {
            _router.Open(UIRouter.UIId.Inventory);
            _router.Open(UIRouter.UIId.Save);

            Assert.IsFalse(_inventory.activeSelf, "排他: 前のパネルは閉じる");
            Assert.IsTrue(_save.activeSelf);
        }

        [Test]
        public void Open_None_ClosesAll()
        {
            _router.Open(UIRouter.UIId.Craft);
            _router.Open(UIRouter.UIId.None);

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void CloseAll_ClosesEveryPanel()
        {
            _router.Open(UIRouter.UIId.Character);

            _router.CloseAll();

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Toggle_OpensThenClosesTheSamePanel()
        {
            _router.Toggle(UIRouter.UIId.Character);
            Assert.IsTrue(_character.activeSelf);

            _router.Toggle(UIRouter.UIId.Character);
            Assert.IsFalse(_character.activeSelf, "同じものを押したら閉じる");
        }

        [Test]
        public void Toggle_SwitchesWhenAnotherPanelIsOpen()
        {
            _router.Open(UIRouter.UIId.Character);

            _router.Toggle(UIRouter.UIId.Inventory);

            Assert.IsFalse(_character.activeSelf);
            Assert.IsTrue(_inventory.activeSelf);
        }

        [Test]
        public void Toggle_AfterPanelClosedItself_ReopensIt()
        {
            // パネル側の戻るボタンで閉じた状態(activeSelf=false)からでも整合する。
            _router.Open(UIRouter.UIId.Save);
            _save.SetActive(false);

            _router.Toggle(UIRouter.UIId.Save);

            Assert.IsTrue(_save.activeSelf);
        }

        [Test]
        public void CraftUiRequest_OpensCraftPanel()
        {
            TestReflection.Invoke(_router, "OnEnable");
            try
            {
                _router.Open(UIRouter.UIId.Inventory);

                CraftUIRequestService.Request();

                Assert.IsTrue(_craft.activeSelf);
                Assert.IsFalse(_inventory.activeSelf, "排他: 前のパネルは閉じる");
            }
            finally
            {
                TestReflection.Invoke(_router, "OnDisable");
            }
        }

        [Test]
        public void CraftUiRequest_AfterDisable_DoesNothing()
        {
            TestReflection.Invoke(_router, "OnEnable");
            TestReflection.Invoke(_router, "OnDisable");

            CraftUIRequestService.Request();

            Assert.IsFalse(_craft.activeSelf);
        }

        [Test]
        public void CraftUiRequest_WithoutCraftPanel_KeepsOtherPanelsOpen()
        {
            var noCraftGo = new GameObject("UIRootWithoutCraft");
            try
            {
                var router = noCraftGo.AddComponent<UIRouter>(); // 調合パネル未割当のルーター
                TestReflection.SetField(router, "_inventoryUI", _inventory);
                TestReflection.Invoke(router, "Awake");
                TestReflection.Invoke(router, "OnEnable");
                router.Open(UIRouter.UIId.Inventory);

                CraftUIRequestService.Request();

                Assert.IsTrue(_inventory.activeSelf);
                TestReflection.Invoke(router, "OnDisable");
            }
            finally
            {
                Object.DestroyImmediate(noCraftGo);
            }
        }

        [Test]
        public void Open_TellsUiPanelServiceItIsOpen()
        {
            _router.Open(UIRouter.UIId.Character);

            Assert.IsTrue(UIPanelService.IsOpen);
        }

        [Test]
        public void CloseAll_TellsUiPanelServiceItIsClosed()
        {
            _router.Open(UIRouter.UIId.Character);

            _router.CloseAll();

            Assert.IsFalse(UIPanelService.IsOpen);
        }

        [Test]
        public void Toggle_Closing_TellsUiPanelServiceItIsClosed()
        {
            _router.Toggle(UIRouter.UIId.Inventory);
            _router.Toggle(UIRouter.UIId.Inventory);

            Assert.IsFalse(UIPanelService.IsOpen);
        }

        [Test]
        public void PanelClosedItself_IsPickedUpByActivityRelay()
        {
            // 戻るボタンはルータを通らずに SetActive(false) で閉じる。
            _router.Open(UIRouter.UIId.Save);
            _save.SetActive(false);

            // EditMode では OnDisable が自動で走らないので、パネルに付いた通知役を明示的に呼ぶ。
            TestReflection.Invoke(_save.GetComponent<UIPanelActivityRelay>(), "OnDisable");

            Assert.IsFalse(UIPanelService.IsOpen);
        }

        [Test]
        public void UnassignedPanel_IsIgnored()
        {
            // 調合UI 未割当(_craftUI = null)でも例外にならない。
            var go = new GameObject("Router2");
            try
            {
                var router = go.AddComponent<UIRouter>();
                TestReflection.SetField(router, "_characterUI", _character);
                TestReflection.Invoke(router, "Awake");

                Assert.DoesNotThrow(() => router.Open(UIRouter.UIId.Craft));
                Assert.IsFalse(router.IsAnyPanelOpen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
