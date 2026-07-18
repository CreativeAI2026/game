using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Core.SceneManagement;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.TitleUI
{
    public class TitleUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _tapToStartButton; // 「はじめる」(新規開始)

        [SerializeField]
        private Button _continueButton; // 「続きから」(セーブ復元)。未割当なら続きから導線は無効

        [SerializeField]
        private string _nextSceneName = SceneNames.FieldArea01;

        [SerializeField]
        private GameStarter _gameStarter; // Title に置く GameStarter(PlayerRig 生成)。未割当なら生成スキップ

        [SerializeField]
        private GameObject _uiRootPrefab; // セッション常駐の UI レイヤー(UIRoot Prefab)。会話UI・即時食材使用UI も UIRoot が束ねる(§6)。未割当なら UI は出ない

        private void Awake()
        {
            if (_tapToStartButton == null)
            {
                Debug.LogError("[TitleUIController] _tapToStartButton is not assigned.");
                return;
            }
            _tapToStartButton.onClick.AddListener(OnTapToStart);

            // 続きから: セーブが在るときだけ押せる。ボタン未割当なら何もしない(導線なし)。
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinue);
                _continueButton.interactable = SaveService.HasSave();
            }
        }

        private void OnDestroy()
        {
            if (_tapToStartButton != null)
            {
                _tapToStartButton.onClick.RemoveListener(OnTapToStart);
            }
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnContinue);
            }
        }

        /// <summary>「はじめる」: セッション常駐を新規生成してフィールドへ(所持品はまっさら)。</summary>
        private void OnTapToStart()
        {
            if (!EnsureSessionAndPlayer())
                return;

            _tapToStartButton.interactable = false;
            SceneController.Instance.LoadScene(_nextSceneName);
        }

        /// <summary>「続きから」: セッション常駐を生成→セーブ復元→保存シーンへ。ロード後にプレイヤーを保存座標へ配置。</summary>
        private void OnContinue()
        {
            if (!EnsureSessionAndPlayer())
                return;

            // 進行度・フラグ・所持品はここで同期復元される(ItemDB は Resources 経由でシーン非依存)。
            var data = SaveService.Load();

            // 座標・現在HP はシーンロード後でないと配置できないため、完了コールバックで復元する。
            string scene = !string.IsNullOrEmpty(data?.sceneName) ? data.sceneName : _nextSceneName;

            _tapToStartButton.interactable = false;
            if (_continueButton != null)
                _continueButton.interactable = false;

            SceneController.Instance.LoadScene(
                scene,
                onSceneActivated: () => SaveService.RestorePlayerState(data)
            );
        }

        /// <summary>
        /// SceneController の存在確認と、セッション常駐の生成を行う。
        /// 生成順は マネージャ → Inventory → プレイヤー(プレイヤーが GameModeManager を購読し、Start で Inventory を読むため。spec §6.1)。
        /// </summary>
        private bool EnsureSessionAndPlayer()
        {
            if (SceneController.Instance == null)
            {
                Debug.LogError(
                    "[TitleUIController] SceneController.Instance is null. Title シーンに PersistentSystems(SceneController)がありません。"
                );
                return false;
            }

            SessionBootstrap.EnsureSession(); // ① マネージャ(ProgressManager / GameModeManager / EventPlayer)
            InventoryManager.EnsureResident(); // ② 所持品(Core は Gameplay を参照できないためここで生成)
            RecipeBookManager.EnsureResident(); // ②' レシピ解禁状態(セッション常駐・セーブ対象。Inventory と同じ層でここで生成)
            UIRoot.EnsureResident(_uiRootPrefab); // ③ UI レイヤー(会話UI・即時食材使用UI を子として同梱=§6。GameModeManager 生成後=HudIconBar 購読可・InventoryManager 生成後=QuickFood 枠購読可・ConversationView が DialogueViewService seam に自己登録)
            BattleRunnerService.Current ??= new BattleRunner(); // ④ 戦闘実行(状態なしの plain class。battle ステップの seam に登録)
            if (_gameStarter != null)
                _gameStarter.EnsurePlayer(); // ⑤ プレイヤーリグ
            return true;
        }
    }
}
