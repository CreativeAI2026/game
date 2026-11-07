using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Core
{
    [DefaultExecutionOrder(-100)]
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [SerializeField, Min(0f)]
        private float _minDisplaySeconds = 0.6f;

        [SerializeField, Min(0f)]
        private float _fadeSeconds = 0.3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip(
            "プログレスバーのうち、本体のあとに重ねて読むシーン(小物など)に割く割合。"
                + "Field_Area01 は本体と重ねるシーンのファイルの大きさがおよそ 2:8 なので 0.8。"
                + "重ねるシーンの無い遷移では、本体が読み終わった時点で 100% へ飛ぶ"
        )]
        private float _additiveLoadShare = 0.8f;

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
        /// 対象シーンが有効化され、それにつられて始まった追加の読み込み(<see cref="PendingSceneLoads"/>)も
        /// 終わったあと(オーバーレイを閉じる前)に一度だけ呼ばれる。
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
            // 読めないシーン名だと LoadSceneAsync が null を返し、暗転したまま止まるので先に弾く
            if (!CanLoad(sceneName))
            {
                Debug.LogError(
                    $"[SceneController] Build Settings に無いシーンは読み込めません: {sceneName}"
                );
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, onSceneActivated));
        }

        public static bool CanLoad(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        private IEnumerator LoadRoutine(string sceneName, System.Action onSceneActivated)
        {
            _isLoading = true;
            // 前の遷移やエディタでの直接再生で登録された読み込みを持ち越さない
            PendingSceneLoads.Clear();

            if (_overlay != null)
                yield return _overlay.ShowCoroutine(_fadeSeconds);

            // 画面は暗転の下なので、フレームの滑らかさより読み込みの速さを優先する
            var loadingPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            float mainShare = 1f - _additiveLoadShare;
            float startTime = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                _overlay?.SetProgress(mainShare * op.progress / 0.9f);
                yield return null;
            }
            _overlay?.SetProgress(mainShare);

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < _minDisplaySeconds)
            {
                yield return new WaitForSecondsRealtime(_minDisplaySeconds - elapsed);
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
                yield return null;

            // 本体の有効化(Awake)で始まった追加の読み込み(小物シーンなど)を、暗転の下で待ち切る。
            while (!PendingSceneLoads.AllDone)
            {
                _overlay?.SetProgress(
                    mainShare + _additiveLoadShare * PendingSceneLoads.Progress01
                );
                yield return null;
            }
            PendingSceneLoads.Clear();
            _overlay?.SetProgress(1f);
            Application.backgroundLoadingPriority = loadingPriority;

            // 重ねるシーンまで揃ったあと・暗転を明ける前にロード後処理(プレイヤー配置など)を実行する。
            onSceneActivated?.Invoke();

            if (_overlay != null)
                yield return _overlay.HideCoroutine(_fadeSeconds);

            _isLoading = false;
        }
    }

    /// <summary>
    /// 遷移中に出す暗転・読み込み表示の窓口。実体は UI 側(LoadingOverlayController)で、
    /// Core は UI を参照できないためこの契約を挟む。SceneController が子から探して使う。
    /// </summary>
    public interface ILoadingOverlay
    {
        IEnumerator ShowCoroutine(float duration);
        IEnumerator HideCoroutine(float duration);
        void SetProgress(float progress01);
    }

    public static class SceneNames
    {
        /// <summary>Title の遷移先の初期値。</summary>
        public const string FieldArea01 = "Field_Area01";
    }
}
