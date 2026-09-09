using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キャラクターUIのタブ切替と、装備品・即時使用食材タブの操作の検証。
    /// 「選択中のタブだけ生きる」ルーティングと、タブ上の操作が InventoryManager(単一ソース)へ
    /// 届くことを見る(documents/Specification.md §5)。見た目・アニメーションは対象外。
    /// </summary>
    public class CharacterUITests
    {
        private sealed class FakeTabView : MonoBehaviour, ICharacterTabView
        {
            public int Initialized;
            public int Entered;
            public int Exited;
            public int Reset;

            public void EnsureInitialized() => Initialized++;

            public void OnEnter() => Entered++;

            public void OnExit() => Exited++;

            public void ResetViewState() => Reset++;
        }

        private GameObject _uiGo;
        private CharacterUIController _ui;
        private GameObject _viewA;
        private GameObject _viewB;
        private FakeTabView _tabA;
        private FakeTabView _tabB;

        private GameObject _invGo;
        private InventoryManager _inv;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("INV");
            _inv = _invGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inv);

            _uiGo = new GameObject("CharacterUI");
            _ui = _uiGo.AddComponent<CharacterUIController>();

            _viewA = new GameObject("StatusView");
            _viewA.transform.SetParent(_uiGo.transform);
            _tabA = _viewA.AddComponent<FakeTabView>();

            _viewB = new GameObject("EquipmentView");
            _viewB.transform.SetParent(_uiGo.transform);
            _tabB = _viewB.AddComponent<FakeTabView>();

            // Start で組む一覧を直接入れる(TabGroup は Inspector 配線なので EditMode では組み立てない)。
            var views = TestReflection.GetField<List<ICharacterTabView>>(_ui, "_tabViews");
            views.Clear();
            views.Add(_tabA);
            views.Add(_tabB);
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_invGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private void SelectView(GameObject selected) =>
            TestReflection.Invoke(_ui, "OnSelectionChanged", 0, null, selected);

        private EquipmentData MakeEquipment(int id)
        {
            var e = ScriptableObject.CreateInstance<EquipmentData>();
            e.id = id;
            _assets.Add(e);
            return e;
        }

        private FoodData MakeFood(int id)
        {
            var f = ScriptableObject.CreateInstance<FoodData>();
            f.id = id;
            _assets.Add(f);
            return f;
        }

        private ItemStack StackOf(ItemData data) => _inv.GetAllItems().Find(s => s.Data == data);

        // --- タブ切替 ---

        [Test]
        public void SelectingATab_EntersOnlyThatView()
        {
            SelectView(_viewA);

            Assert.AreEqual(1, _tabA.Entered);
            Assert.AreEqual(0, _tabA.Exited);
            Assert.AreEqual(1, _tabB.Exited, "選択されていないタブは OnExit");
            Assert.AreEqual(0, _tabB.Entered);
        }

        [Test]
        public void SwitchingTabs_ExitsThePreviousOne()
        {
            SelectView(_viewA);
            SelectView(_viewB);

            Assert.AreEqual(1, _tabB.Entered);
            Assert.AreEqual(1, _tabA.Exited, "前のタブは閉じられる(同時に2枚生きない)");
        }

        [Test]
        public void SelectingAChildOfTheView_CountsAsThatTab()
        {
            // タブのビューは入れ子になることがあるので、子コンポーネントでも「そのタブ」と判定する。
            var child = new GameObject("Child");
            child.transform.SetParent(_viewA.transform);
            var childTab = child.AddComponent<FakeTabView>();
            TestReflection.GetField<List<ICharacterTabView>>(_ui, "_tabViews").Add(childTab);

            SelectView(_viewA);

            Assert.AreEqual(1, childTab.Entered);
            Assert.AreEqual(0, childTab.Exited);
        }

        [Test]
        public void SelectingNothing_ExitsEveryTab()
        {
            SelectView(_viewA);

            SelectView(null);

            Assert.AreEqual(1, _tabA.Exited);
            Assert.AreEqual(2, _tabB.Exited);
        }

        // --- 装備品タブ: 装備変更が InventoryManager に届く(spec §5「装備品タブで装備品の装備変更」) ---

        [Test]
        public void EquipmentTab_EquipAndUnequip_GoThroughInventoryManager()
        {
            var gear = MakeEquipment(2001);
            _inv.AddItem(gear, 1);
            var stack = StackOf(gear);

            var viewGo = new GameObject("EquipmentViewController");
            try
            {
                var view = viewGo.AddComponent<EquipmentViewController>();
                TestReflection.Invoke(view, "UnequipStack", stack, false);
                Assert.IsFalse(stack.IsEquipped); // 前提

                _inv.SetEquipped(stack, true);
                Assert.IsTrue(stack.IsEquipped);

                TestReflection.Invoke(view, "UnequipStack", stack, false);

                Assert.IsFalse(stack.IsEquipped, "UI からの解除が単一ソースへ届く");
            }
            finally
            {
                Object.DestroyImmediate(viewGo);
            }
        }

        [Test]
        public void EquipmentTab_CannotExceedThreeEquipped()
        {
            // UI 経由でも上限3は InventoryManager 側で守られる(spec §2.1)。
            var stacks = new List<ItemStack>();
            for (int i = 0; i < 4; i++)
            {
                var gear = MakeEquipment(2100 + i);
                _inv.AddItem(gear, 1);
                stacks.Add(StackOf(gear));
            }

            foreach (var s in stacks)
                _inv.SetEquipped(s, true);

            Assert.AreEqual(3, _inv.GetAllItems().Count(s => s.IsEquipped));
        }

        // --- 即時使用食材タブ: 3枠のセット(spec §5「即時使用食材タブで最大3つのセット」) ---

        [Test]
        public void QuickFoodTab_SetsFoodIntoTheFirstEmptySlot()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);

            Assert.IsTrue(_inv.SetQuickFood(0, StackOf(apple)));

            var slots = _inv.GetQuickFoodSlots();
            Assert.AreEqual(3, slots.Count, "枠は最大3つ");
            Assert.AreSame(StackOf(apple), slots[0]);
        }

        [Test]
        public void QuickFoodTab_ClearSlot_EmptiesIt()
        {
            var apple = MakeFood(3001);
            _inv.AddItem(apple, 1);
            _inv.SetQuickFood(2, StackOf(apple));

            _inv.ClearQuickFood(2);

            Assert.IsNull(_inv.GetQuickFoodSlots()[2]);
        }

        [Test]
        public void QuickFoodTab_RejectsNonFoodAndOutOfRange()
        {
            var gear = MakeEquipment(2001);
            var apple = MakeFood(3001);
            _inv.AddItem(gear, 1);
            _inv.AddItem(apple, 1);

            Assert.IsFalse(_inv.SetQuickFood(0, StackOf(gear)), "食材以外は枠に入らない");
            Assert.IsFalse(_inv.SetQuickFood(3, StackOf(apple)), "枠は 0..2 のみ");
        }
    }
}
