using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class TitleUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _tapToStartButton; // 「はじめる」(新規開始)

        [SerializeField]
        private Button _continueButton; // 「続きから」(セーブ復元)。未割当なら続きから導線は無効

        [SerializeField]
        private string _nextSceneName = SceneNames.FieldArea00;

        [SerializeField]
        private GameObject _playerRigPrefab; // 「はじめる/続きから」で生成するプレイヤーリグ。未割当なら警告してプレイヤーは出ない

        [SerializeField]
        private GameObject _uiRootPrefab; // セッション常駐の UI レイヤー(UIRoot Prefab)。会話UI・即時食材使用UI も UIRoot が束ねる。未割当なら UI は出ない

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

            GameSession.EnsureResidents(_uiRootPrefab);
            GameSession.EnsurePlayerRig(_playerRigPrefab);
            return true;
        }
    }
}
