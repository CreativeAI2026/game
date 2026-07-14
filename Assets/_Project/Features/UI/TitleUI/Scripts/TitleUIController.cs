using CreativeAI.Core;
using CreativeAI.Core.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.TitleUI
{
    public class TitleUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _tapToStartButton;

        [SerializeField]
        private string _nextSceneName = SceneNames.FieldArea01;

        [SerializeField]
        private GameStarter _gameStarter; // Title に置く GameStarter(PlayerRig 生成)。未割当なら生成スキップ

        private void Awake()
        {
            if (_tapToStartButton == null)
            {
                Debug.LogError("[TitleUIController] _tapToStartButton is not assigned.");
                return;
            }
            _tapToStartButton.onClick.AddListener(OnTapToStart);
        }

        private void OnDestroy()
        {
            if (_tapToStartButton != null)
            {
                _tapToStartButton.onClick.RemoveListener(OnTapToStart);
            }
        }

        private void OnTapToStart()
        {
            if (SceneController.Instance == null)
            {
                Debug.LogError(
                    "[TitleUIController] SceneController.Instance is null. Title シーンに PersistentSystems(SceneController)がありません。"
                );
                return;
            }
            // 「はじめる」でセッション常駐を生成してからフィールドへ。
            // 生成順は マネージャ → プレイヤー(プレイヤーが GameModeManager を購読するため。spec §6.1)。
            SessionBootstrap.EnsureSession(); // ① マネージャ
            if (_gameStarter != null)
                _gameStarter.EnsurePlayer(); // ② プレイヤーリグ

            _tapToStartButton.interactable = false;
            SceneController.Instance.LoadScene(_nextSceneName);
        }
    }
}
