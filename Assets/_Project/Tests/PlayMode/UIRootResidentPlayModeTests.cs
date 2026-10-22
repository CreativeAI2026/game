using System.Collections;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// 本物の UIRoot で常駐UI(会話UI・扉プロンプト・即時食材バー等)が登録・表示されるかの検証。
    /// UIRoot は本番と同じ経路(ResidentBootstrapConfig → GameSession.EnsureResidents)で生成する。
    /// 部品ごとのテストは手組みの GameObject で回るので、Prefab 側の保存状態や配線の崩れはここでしか拾えない。
    /// </summary>
    public class UIRootResidentPlayModeTests
    {
        private UIRoot _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var config = Resources.Load<ResidentBootstrapConfig>(nameof(ResidentBootstrapConfig));
            Assert.IsNotNull(config, "Resources/ResidentBootstrapConfig が見つかりません。");
            Assert.IsNotNull(
                config.uiRootPrefab,
                "ResidentBootstrapConfig.uiRootPrefab が未割当です。"
            );

            GameSession.EnsureResidents(config.uiRootPrefab);
            _root = UIRoot.Instance;
            Assert.IsNotNull(_root, "UIRoot が生成されていません。");
            yield return null; // Update での表示判定を1回回す
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InteractPromptService.Clear();
            EventPlaybackService.SetPlaying(false);
            BattleRunnerService.Current = null;
            if (_root != null)
                Object.Destroy(_root.gameObject);
            DestroyResident<ProgressManager>();
            DestroyResident<GameModeManager>();
            DestroyResident<InventoryManager>();
            DestroyResident<EventPlayer>();
            yield return null; // Destroy の反映を待つ(次のテストへ持ち越さない)
        }

        private static void DestroyResident<T>()
            where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude))
                Object.Destroy(found.gameObject);
        }

        private T Find<T>()
            where T : Component
        {
            var found = _root.GetComponentInChildren<T>(true);
            Assert.IsNotNull(found, $"UIRoot に {typeof(T).Name} がありません。");
            return found;
        }

        private static bool IsShown(Component ui)
        {
            var canvas = ui.GetComponent<Canvas>();
            return ui.gameObject.activeInHierarchy && canvas != null && canvas.enabled;
        }

        [Test]
        public void ConversationView_RegistersAsDialogueView()
        {
            var view = Find<ConversationView>();

            Assert.IsTrue(
                view.gameObject.activeInHierarchy,
                "ConversationView がオフのままです(Awake が走らず会話UIとして登録されません)。"
            );
            Assert.AreSame(view, DialogueViewService.Current);
        }

        [UnityTest]
        public IEnumerator InteractPrompt_ShowsLabel_AndHidesAgain()
        {
            var prompt = Find<InteractPromptView>();
            var owner = new object();
            Assert.IsFalse(IsShown(prompt), "何も出していないのにプロンプトが表示されています。");

            InteractPromptService.Show(owner, "[E] 扉を開ける");
            yield return null;

            Assert.IsTrue(IsShown(prompt), "Show してもプロンプトが表示されません。");
            var label = prompt.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(label, "プロンプトにラベル(TMP_Text)がありません。");
            Assert.IsTrue(
                label.gameObject.activeInHierarchy,
                "プロンプトのラベルがオフのままです。"
            );
            Assert.AreEqual("[E] 扉を開ける", label.text);

            InteractPromptService.Hide(owner);
            yield return null;

            Assert.IsFalse(IsShown(prompt), "Hide してもプロンプトが消えません。");
        }

        [UnityTest]
        public IEnumerator OpeningAPanel_HidesPromptAndQuickFoodBar_ButKeepsIconBar()
        {
            var prompt = Find<InteractPromptView>();
            var quickFood = Find<QuickFoodBarController>();
            var iconBar = Find<HudIconBar>();
            var router = _root.GetComponent<UiRouter>();
            InteractPromptService.Show(this, "[E] 扉を開ける");
            yield return null;

            Assert.IsTrue(IsShown(prompt), "パネルを開く前にプロンプトが表示されていません。");
            Assert.IsTrue(IsShown(quickFood), "パネルを開く前に即時食材バーが表示されていません。");
            Assert.IsTrue(IsShown(iconBar), "パネルを開く前にアイコンバーが表示されていません。");

            router.Open(UiRouter.UiId.Inventory);
            yield return null;

            Assert.IsTrue(router.IsAnyPanelOpen);
            Assert.IsFalse(IsShown(prompt), "パネル表示中にプロンプトが残っています。");
            Assert.IsFalse(IsShown(quickFood), "パネル表示中に即時食材バーが残っています。");
            Assert.IsTrue(
                IsShown(iconBar),
                "パネル表示中はアイコンバーから閉じられるよう表示したままにします。"
            );

            router.CloseAll();
            yield return null;

            Assert.IsTrue(IsShown(prompt), "パネルを閉じてもプロンプトが戻りません。");
            Assert.IsTrue(IsShown(quickFood), "パネルを閉じても即時食材バーが戻りません。");
        }

        [UnityTest]
        public IEnumerator DuringConversation_HudIsHidden_AndReturnsAfterwards()
        {
            var prompt = Find<InteractPromptView>();
            var quickFood = Find<QuickFoodBarController>();
            var iconBar = Find<HudIconBar>();
            InteractPromptService.Show(this, "[E] 扉を開ける");
            yield return null;

            EventPlaybackService.SetPlaying(true);
            yield return null;

            Assert.IsFalse(IsShown(prompt), "会話中にプロンプトが残っています。");
            Assert.IsFalse(IsShown(quickFood), "会話中に即時食材バーが残っています。");
            Assert.IsFalse(IsShown(iconBar), "会話中にアイコンバーが残っています。");

            EventPlaybackService.SetPlaying(false);
            yield return null;

            Assert.IsTrue(IsShown(prompt), "会話が終わってもプロンプトが戻りません。");
            Assert.IsTrue(IsShown(quickFood), "会話が終わっても即時食材バーが戻りません。");
            Assert.IsTrue(IsShown(iconBar), "会話が終わってもアイコンバーが戻りません。");
        }
    }
}
