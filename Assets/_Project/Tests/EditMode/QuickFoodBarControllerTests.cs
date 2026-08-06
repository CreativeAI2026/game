using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.UI;
using CreativeAI.UI.QuickFoodBar;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 即時食材使用UIの出し分け(documents/Specification.md §5, §2.2)。
    /// 「移動中・戦闘中とも常時表示」で、隠すのは会話中とパネル(インベ/キャラ/セーブ/調合)表示中だけ。
    /// battle ステップも EventPlaybackService.IsPlaying の内側で走るので、モードで戦闘を除外する必要がある。
    /// </summary>
    public class QuickFoodBarControllerTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _rootGo;
        private UiRouter _router;
        private GameObject _panel;
        private GameObject _barGo;
        private QuickFoodBarController _bar;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);

            _rootGo = new GameObject("UIRoot");
            _router = _rootGo.AddComponent<UiRouter>();
            _panel = new GameObject("InventoryUI");
            TestReflection.SetField(_router, "_inventoryUI", _panel);
            TestReflection.Invoke(_router, "Awake"); // パネル登録 + 全閉じ

            _barGo = new GameObject("QuickFoodBar");
            _barGo.transform.SetParent(_rootGo.transform);
            _canvas = _barGo.AddComponent<Canvas>();
            _raycaster = _barGo.AddComponent<GraphicRaycaster>();
            _bar = _barGo.AddComponent<QuickFoodBarController>();

            // EditMode では Awake が走らないので、Awake が解決する参照を直接入れる。
            TestReflection.SetField(_bar, "_canvas", _canvas);
            TestReflection.SetField(_bar, "_raycaster", _raycaster);
            TestReflection.SetField(_bar, "_router", _router);
        }

        [TearDown]
        public void TearDown()
        {
            EventPlaybackService.SetPlaying(false);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_barGo);
            Object.DestroyImmediate(_panel);
            Object.DestroyImmediate(_rootGo);
            Object.DestroyImmediate(_gmmGo);
        }

        /// <summary>Update から呼ばれる表示判定を直接叩く。</summary>
        private bool Visible()
        {
            TestReflection.Invoke(_bar, "ApplyVisibility");
            return _canvas.enabled && _raycaster.enabled;
        }

        [Test]
        public void FieldMode_IsShown()
        {
            Assert.IsTrue(Visible());
        }

        [Test]
        public void BattleMode_IsStillShown()
        {
            _gmm.EnterBattle();

            Assert.IsTrue(Visible(), "戦闘中も常時表示(右上アイコンバーと違いモードで消さない)");
        }

        [Test]
        public void DuringDialogue_IsHidden()
        {
            EventPlaybackService.SetPlaying(true);

            Assert.IsFalse(Visible(), "会話(line/choice)中は使用不可なので隠す");
        }

        [Test]
        public void DuringBattleStepOfAnEvent_IsShown()
        {
            // battle ステップはイベント再生中(IsPlaying=true)かつ Battle モード。ここは戦闘なので表示する。
            EventPlaybackService.SetPlaying(true);
            _gmm.EnterBattle();

            Assert.IsTrue(Visible(), "イベント中の戦闘は「戦闘中」扱いで表示する");
        }

        [Test]
        public void AfterBattleReturnsToDialogue_IsHiddenAgain()
        {
            EventPlaybackService.SetPlaying(true);
            _gmm.EnterBattle();
            Assert.IsTrue(Visible());

            _gmm.ExitBattle(); // 戦闘が終わって会話に戻る

            Assert.IsFalse(Visible());
        }

        [Test]
        public void WhenAPanelIsOpen_IsHidden()
        {
            _router.Open(UiRouter.UiId.Inventory);

            Assert.IsFalse(Visible(), "インベ等を開いている間は隠す");
        }

        [Test]
        public void AfterClosingPanels_IsShownAgain()
        {
            _router.Open(UiRouter.UiId.Inventory);
            Assert.IsFalse(Visible());

            _router.CloseAll();

            Assert.IsTrue(Visible());
        }
    }
}
