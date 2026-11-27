using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// UIManager で、操作で開く UI(キャラ / インベ / セーブ / 調合)が1つずつしか開かないことの検証。
    /// 開くのは常に1つで、別のを開くと前のが閉じる。
    /// どれかが開いている間はメニュー中(<see cref="GameModeManager.IsMenuOpen"/>)になる(プレイヤーの操作を止めるため)。
    /// </summary>
    public class UIManagerTests
    {
        private readonly TestResidents _residents = new();
        private GameModeManager _gmm;
        private GameObject _rootGo;
        private UIManager _router;
        private GameObject _character;
        private GameObject _inventory;
        private GameObject _save;
        private GameObject _craft;

        [SetUp]
        public void SetUp()
        {
            _gmm = _residents.Add<GameModeManager>();
            _rootGo = new GameObject("UIRoot");
            _router = _rootGo.AddComponent<UIManager>();

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
            _residents.Clear();
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
            _router.Open(UIManager.UIId.Inventory);

            Assert.IsTrue(_inventory.activeSelf);
            Assert.IsFalse(_character.activeSelf);
            Assert.IsFalse(_save.activeSelf);
            Assert.IsFalse(_craft.activeSelf);
            Assert.IsTrue(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Open_Another_ClosesThePrevious()
        {
            _router.Open(UIManager.UIId.Inventory);
            _router.Open(UIManager.UIId.Save);

            Assert.IsFalse(_inventory.activeSelf, "排他: 前のパネルは閉じる");
            Assert.IsTrue(_save.activeSelf);
        }

        [Test]
        public void Open_None_ClosesAll()
        {
            _router.Open(UIManager.UIId.Craft);
            _router.Open(UIManager.UIId.None);

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void CloseAll_ClosesEveryPanel()
        {
            _router.Open(UIManager.UIId.Character);

            _router.CloseAll();

            Assert.IsFalse(_router.IsAnyPanelOpen);
        }

        [Test]
        public void Toggle_OpensThenClosesTheSamePanel()
        {
            _router.Toggle(UIManager.UIId.Character);
            Assert.IsTrue(_character.activeSelf);

            _router.Toggle(UIManager.UIId.Character);
            Assert.IsFalse(_character.activeSelf, "同じものを押したら閉じる");
        }

        [Test]
        public void Toggle_SwitchesWhenAnotherPanelIsOpen()
        {
            _router.Open(UIManager.UIId.Character);

            _router.Toggle(UIManager.UIId.Inventory);

            Assert.IsFalse(_character.activeSelf);
            Assert.IsTrue(_inventory.activeSelf);
        }

        [Test]
        public void Toggle_AfterPanelClosedItself_ReopensIt()
        {
            // パネル側の戻るボタンで閉じた状態(activeSelf=false)からでも整合する。
            _router.Open(UIManager.UIId.Save);
            _save.SetActive(false);

            _router.Toggle(UIManager.UIId.Save);

            Assert.IsTrue(_save.activeSelf);
        }

        // 合成装置の範囲内で「合成する」を押す
        private static void PressCraftStation()
        {
            var go = new GameObject("CraftStation", typeof(BoxCollider));
            try
            {
                var station = go.AddComponent<CraftStationInteractor>();
                TestReflection.SetField(station, "_playerInside", true);
                Assert.IsTrue(station.TryInteract());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CraftUiRequest_OpensCraftPanel()
        {
            TestReflection.Invoke(_router, "OnEnable");
            try
            {
                PressCraftStation();

                Assert.IsTrue(_craft.activeSelf);
                Assert.IsTrue(_gmm.IsMenuOpen);
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

            PressCraftStation();

            Assert.IsFalse(_craft.activeSelf);
        }

        [Test]
        public void CraftUiRequest_WithoutCraftPanel_DoesNothing()
        {
            var noCraftGo = new GameObject("UIRootWithoutCraft");
            try
            {
                var router = noCraftGo.AddComponent<UIManager>(); // 調合パネル未割当のルーター
                TestReflection.SetStaticProperty<UIManager>("Instance", null); // 2つ目として捨てられないように
                TestReflection.SetField(router, "_inventoryUI", _inventory);
                TestReflection.Invoke(router, "Awake");
                TestReflection.Invoke(router, "OnEnable");

                PressCraftStation();

                Assert.IsFalse(router.IsAnyPanelOpen);
                Assert.IsFalse(_gmm.IsMenuOpen);
                TestReflection.Invoke(router, "OnDisable");
            }
            finally
            {
                Object.DestroyImmediate(noCraftGo);
            }
        }

        [Test]
        public void Open_OpensMenu()
        {
            _router.Open(UIManager.UIId.Character);

            Assert.IsTrue(_gmm.IsMenuOpen);
        }

        [Test]
        public void CloseAll_ClosesMenu()
        {
            _router.Open(UIManager.UIId.Character);

            _router.CloseAll();

            Assert.IsFalse(_gmm.IsMenuOpen);
        }

        [Test]
        public void Toggle_Closing_ClosesMenu()
        {
            _router.Toggle(UIManager.UIId.Inventory);
            _router.Toggle(UIManager.UIId.Inventory);

            Assert.IsFalse(_gmm.IsMenuOpen);
        }

        [Test]
        public void SwitchingPanels_StaysInMenu()
        {
            var changes = new System.Collections.Generic.List<bool>();
            _gmm.Changed += () => changes.Add(_gmm.IsMenuOpen);

            _router.Open(UIManager.UIId.Character);
            _router.Open(UIManager.UIId.Inventory); // 開いたまま切り替えてもモードは変わらない
            _router.CloseAll();

            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }

        [Test]
        public void ClosingPanel_DuringBattle_KeepsBattleMode()
        {
            _router.Open(UIManager.UIId.Inventory);
            _gmm.TryStartBattle(); // 戦闘が始まった(アイコンバーがパネルを閉じる)

            _router.CloseAll();

            Assert.AreEqual(GameMode.Battle, _gmm.Mode, "閉じても戦闘を Field に戻さない");
        }

        [Test]
        public void PanelClosedItself_IsPickedUpByActivityRelay()
        {
            // 戻るボタンはルータを通らずに SetActive(false) で閉じる。
            _router.Open(UIManager.UIId.Save);
            _save.SetActive(false);

            // EditMode では OnDisable が自動で走らないので、パネルに付いた通知役を明示的に呼ぶ。
            TestReflection.Invoke(_save.GetComponent<UIPanelActivityRelay>(), "OnDisable");

            Assert.IsFalse(_gmm.IsMenuOpen);
        }

        [Test]
        public void UnassignedPanel_IsIgnored()
        {
            // 調合UI 未割当(_craftUI = null)でも例外にならない。
            var go = new GameObject("Router2");
            try
            {
                var router = go.AddComponent<UIManager>();
                TestReflection.SetStaticProperty<UIManager>("Instance", null); // 2つ目として捨てられないように
                TestReflection.SetField(router, "_characterUI", _character);
                TestReflection.Invoke(router, "Awake");

                Assert.DoesNotThrow(() => router.Open(UIManager.UIId.Craft));
                Assert.IsFalse(router.IsAnyPanelOpen);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Open_DuringDialogue_DoesNotOpen()
        {
            _gmm.TryStartDialogue();

            _router.Open(UIManager.UIId.Character);

            Assert.IsFalse(_router.IsAnyPanelOpen, "会話中はメニューを開けない");
            Assert.IsFalse(_gmm.IsMenuOpen);
        }
    }
}
