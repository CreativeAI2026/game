using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Core.SceneManagement
{
    [DefaultExecutionOrder(-100)]
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [SerializeField, Min(0f)]
        private float _minDisplaySeconds = 0.6f;

        [SerializeField, Min(0f)]
        private float _fadeSeconds = 0.3f;

        private ILoadingOverlay _overlay;
        private bool _isLoading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _overlay = GetComponentInChildren<ILoadingOverlay>(includeInactive: true);
        }

        /// <param name="onSceneActivated">
        /// 対象シーンが有効化された直後(オーバーレイを閉じる前)に一度だけ呼ばれる。
        /// セーブ復元でプレイヤーを保存座標へ配置する等、ロード後処理をここで行う(画面はまだ暗幕の下)。
        /// </param>
        public void LoadScene(string sceneName, System.Action onSceneActivated = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning(
                    $"[SceneController] LoadScene ignored (already loading): {sceneName}"
                );
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, onSceneActivated));
        }

        private IEnumerator LoadRoutine(string sceneName, System.Action onSceneActivated)
        {
            _isLoading = true;

            if (_overlay != null)
                yield return _overlay.ShowCoroutine(_fadeSeconds);

            float startTime = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                _overlay?.SetProgress(op.progress / 0.9f);
                yield return null;
            }
            _overlay?.SetProgress(1f);

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < _minDisplaySeconds)
            {
                yield return new WaitForSecondsRealtime(_minDisplaySeconds - elapsed);
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
                yield return null;

            // シーン有効化直後・暗幕を閉じる前にロード後処理(プレイヤー配置など)を実行する。
            onSceneActivated?.Invoke();

            if (_overlay != null)
                yield return _overlay.HideCoroutine(_fadeSeconds);

            _isLoading = false;
        }
    }
}
