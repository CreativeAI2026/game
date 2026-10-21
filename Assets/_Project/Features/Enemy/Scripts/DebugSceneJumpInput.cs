using CreativeAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指定キー（既定: F3）で、設定したテスト用シーンへ遷移するデバッグ用コンポーネント。
    /// エディター・ビルドの両方で動作する。
    /// 遷移先シーンは Build Profiles（Build Settings）のシーン一覧に登録されている必要がある。
    /// </summary>
    public class DebugSceneJumpInput : MonoBehaviour
    {
        [Tooltip("遷移先のシーン名（拡張子なし）。Build Settings のシーン一覧に登録しておくこと")]
        [SerializeField]
        private string _targetSceneName = string.Empty;

        [Tooltip("遷移を実行するキー")]
        [SerializeField]
        private Key _jumpKey = Key.F4;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_jumpKey].wasPressedThisFrame)
                JumpToTargetScene();
        }

        private void JumpToTargetScene()
        {
            if (string.IsNullOrEmpty(_targetSceneName))
            {
                Debug.LogError($"[{nameof(DebugSceneJumpInput)}] 遷移先のシーン名が未設定です", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_targetSceneName))
            {
                Debug.LogError(
                    $"[{nameof(DebugSceneJumpInput)}] シーン '{_targetSceneName}' が Build Settings に登録されていません",
                    this
                );
                return;
            }

            // Title から起動した場合は暗転付きの既存遷移を使い、テストシーンを直接再生した場合などは直接読み込む
            if (SceneController.Instance != null)
                SceneController.Instance.LoadScene(_targetSceneName);
            else
                SceneManager.LoadScene(_targetSceneName);
        }
    }
}
