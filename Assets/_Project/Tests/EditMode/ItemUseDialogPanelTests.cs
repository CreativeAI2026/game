using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using CreativeAI.UI.InventoryUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// インベントリ食材タブからの使用導線の検証(移動中は食材タブからも所持食材を使用できる)。
    /// 仕様は documents/Specification.md §2.2。
    /// ダイアログは食材にだけ出て、使用ボタンで HP回復 + 在庫1消費まで通ることを見る。
    /// </summary>
    public class ItemUseDialogPanelTests
    {
        private GameObject _panelGo;
        private ItemUseDialogPanel _panel;
        private Button _useButton;
        private GameObject _invGo;
        private InventoryManager _inv;
        private GameObject _playerGo;
        private PlayerStatus _status;
        private readonly List<Object> _assets = new();

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject("INV");
            _inv = _invGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inv);

            var playerData = ScriptableObject.CreateInstance<PlayerParameterData>();
            playerData.baseMaxLife = 1000f;
            _assets.Add(playerData);
            _playerGo = new GameObject("Player") { tag = "Player" };
            _status = _playerGo.AddComponent<PlayerStatus>();
            TestReflection.SetField(_status, "_playerData", playerData);

            _panelGo = BuildPanel();
            _panel = _panelGo.GetComponent<ItemUseDialogPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            Object.DestroyImmediate(_panelGo);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_invGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        /// <summary>必須参照が全部揃った最小のダイアログを組む(欠けると自身を Hide してしまうため)。</summary>
        private GameObject BuildPanel()
        {
            var root = new GameObject("ItemUseDialogPanel", typeof(RectTransform));
            var background = root.AddComponent<Image>();
            background.raycastTarget = true;
            var closeOnSelfClick = root.AddComponent<CloseOnSelfClick>();

            var dialogRoot = new GameObject("DialogRoot", typeof(RectTransform));
            dialogRoot.transform.SetParent(root.transform);
            var dialogGraphic = dialogRoot.AddComponent<Image>();
            dialogGraphic.raycastTarget = true;

            var icon = new GameObject("ItemIcon", typeof(RectTransform));
            icon.transform.SetParent(dialogRoot.transform);
            var iconImage = icon.AddComponent<Image>();

            var nameGo = new GameObject("ItemName", typeof(RectTransform));
            nameGo.transform.SetParent(dialogRoot.transform);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();

            var effectGo = new GameObject("EffectText", typeof(RectTransform));
            effectGo.transform.SetParent(dialogRoot.transform);
            var effectText = effectGo.AddComponent<TextMeshProUGUI>();

            var buttonGo = new GameObject("UseButton", typeof(RectTransform));
            buttonGo.transform.SetParent(dialogRoot.transform);
            buttonGo.AddComponent<Image>();
            _useButton = buttonGo.AddComponent<Button>();

            var panel = root.AddComponent<ItemUseDialogPanel>();
            TestReflection.SetField(panel, "_closeOnSelfClick", closeOnSelfClick);
            TestReflection.SetField(panel, "_backgroundImage", background);
            TestReflection.SetField(panel, "_dialogRoot", dialogRoot.GetComponent<RectTransform>());
            TestReflection.SetField(panel, "_itemIconImage", iconImage);
            TestReflection.SetField(panel, "_itemNameText", nameText);
            TestReflection.SetField(panel, "_itemEffectText", effectText);
            TestReflection.SetField(panel, "_useButton", _useButton);
            return root;
        }

        private FoodData MakeFood(int id, string itemName)
        {
            var f = ScriptableObject.CreateInstance<FoodData>();
            f.id = id;
            f.itemName = itemName;
            _assets.Add(f);
            return f;
        }

        private ItemStack StackOf(ItemData data) => _inv.GetAllItems().Find(s => s.Data == data);

        [Test]
        public void Show_Food_OpensDialogWithItsName()
        {
            var apple = MakeFood(3001, "りんご");
            _inv.AddItem(apple, 2);

            _panel.Show(StackOf(apple));

            Assert.IsTrue(_panelGo.activeSelf);
            Assert.AreEqual(
                "りんご",
                TestReflection.GetField<TMP_Text>(_panel, "_itemNameText").text
            );
        }

        [Test]
        public void Show_NonFood_StaysClosed()
        {
            var gear = ScriptableObject.CreateInstance<EquipmentData>();
            gear.id = 2001;
            _assets.Add(gear);
            _inv.AddItem(gear, 1);

            _panel.Show(StackOf(gear));

            Assert.IsFalse(_panelGo.activeSelf, "食材以外では使用ダイアログを出さない");
        }

        [Test]
        public void Show_Null_StaysClosed()
        {
            _panel.Show(null);

            Assert.IsFalse(_panelGo.activeSelf);
        }

        [Test]
        public void UseButton_HealsAndConsumesOne_ThenCloses()
        {
            var apple = MakeFood(3001, "りんご");
            _inv.AddItem(apple, 3);
            _status.RestoreHp(100f);
            _panel.Show(StackOf(apple));

            _useButton.onClick.Invoke();

            Assert.AreEqual(300f, _status.CurrentHp, 1e-2f, "最大HP1000 の 20% 回復");
            Assert.AreEqual(2, StackOf(apple).Count, "在庫が1つ減る");
            Assert.IsFalse(_panelGo.activeSelf, "使用したらダイアログは閉じる");
        }

        [Test]
        public void UseButton_LastOne_RemovesStack()
        {
            var apple = MakeFood(3001, "りんご");
            _inv.AddItem(apple, 1);
            _status.RestoreHp(0f);
            _panel.Show(StackOf(apple));

            _useButton.onClick.Invoke();

            Assert.IsNull(StackOf(apple));
        }

        [Test]
        public void Hide_ClearsTarget_SoUseButtonDoesNothing()
        {
            var apple = MakeFood(3001, "りんご");
            _inv.AddItem(apple, 2);
            _status.RestoreHp(100f);
            _panel.Show(StackOf(apple));

            _panel.Hide();
            _useButton.onClick.Invoke();

            Assert.AreEqual(2, StackOf(apple).Count, "閉じた後のボタン発火で誤使用しない");
            Assert.AreEqual(100f, _status.CurrentHp, 1e-2f);
        }
    }
}
