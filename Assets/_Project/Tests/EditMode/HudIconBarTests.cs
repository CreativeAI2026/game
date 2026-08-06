using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 右上アイコンバーの出し分け(documents/Specification.md §5, §2.2, §0)。
    /// 「移動中のみ表示・戦闘モード中は非表示」+「会話UI表示中も非表示(セーブ/インベを開かせない)」。
    /// GameObject ごと消すと購読が切れるため、Canvas / Raycaster の enabled で出し分ける実装を検証する。
    /// </summary>
    public class HudIconBarTests
    {
        private GameObject _gmmGo;
        private GameModeManager _gmm;
        private GameObject _barGo;
        private HudIconBar _bar;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        [SetUp]
        public void SetUp()
        {
            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);

            _barGo = new GameObject("HudIconBar");
            _canvas = _barGo.AddComponent<Canvas>();
            _raycaster = _barGo.AddComponent<GraphicRaycaster>();
            _bar = _barGo.AddComponent<HudIconBar>();
            TestReflection.SetField(_bar, "_canvas", _canvas);
            TestReflection.SetField(_bar, "_raycaster", _raycaster);

            // EditMode では OnEnable が走らないので、購読 + 初期反映を明示的に行う。
            TestReflection.Invoke(_bar, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_bar, "OnDisable");
            EventPlaybackService.SetPlaying(false);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_barGo);
            Object.DestroyImmediate(_gmmGo);
        }

        private bool IsShown => _canvas.enabled && _raycaster.enabled;

        [Test]
        public void FieldMode_NotInDialogue_IsShown()
        {
            Assert.IsTrue(IsShown);
        }

        [Test]
        public void BattleMode_IsHidden()
        {
            _gmm.EnterBattle();

            Assert.IsFalse(IsShown, "戦闘モード中は非表示(spec §5)");
        }

        [Test]
        public void ReturningToField_ShowsAgain()
        {
            _gmm.EnterBattle();
            Assert.IsFalse(IsShown);

            _gmm.ExitBattle();

            Assert.IsTrue(IsShown);
        }

        [Test]
        public void DuringEventPlayback_IsHidden()
        {
            EventPlaybackService.SetPlaying(true);

            Assert.IsFalse(IsShown, "会話UI表示中はセーブ/インベを開けない(spec §2.2, §0)");
        }

        [Test]
        public void AfterEventPlayback_ShowsAgain()
        {
            EventPlaybackService.SetPlaying(true);
            Assert.IsFalse(IsShown);

            EventPlaybackService.SetPlaying(false);

            Assert.IsTrue(IsShown);
        }

        [Test]
        public void BattleAndDialogue_BothCleared_IsRequiredToShow()
        {
            // 戦闘かつ会話中 → 片方だけ解除しても出さない。
            _gmm.EnterBattle();
            EventPlaybackService.SetPlaying(true);
            Assert.IsFalse(IsShown);

            EventPlaybackService.SetPlaying(false);
            Assert.IsFalse(IsShown, "まだ戦闘中");

            _gmm.ExitBattle();
            Assert.IsTrue(IsShown);
        }

        [Test]
        public void GameObjectStaysActive_SoSubscriptionsSurvive()
        {
            _gmm.EnterBattle();

            Assert.IsTrue(
                _barGo.activeSelf,
                "SetActive(false) で自分を止めると購読が切れて戻れなくなる"
            );
        }

        [Test]
        public void WithoutGameModeManager_DefaultsToShown()
        {
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            var go = new GameObject("HudIconBar2");
            try
            {
                var canvas = go.AddComponent<Canvas>();
                var raycaster = go.AddComponent<GraphicRaycaster>();
                var bar = go.AddComponent<HudIconBar>();
                TestReflection.SetField(bar, "_canvas", canvas);
                TestReflection.SetField(bar, "_raycaster", raycaster);

                TestReflection.Invoke(bar, "OnEnable");

                Assert.IsTrue(canvas.enabled, "マネージャ未生成(タイトル直後)は Field 扱いで表示");
                TestReflection.Invoke(bar, "OnDisable");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
