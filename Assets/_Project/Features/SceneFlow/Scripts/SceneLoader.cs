using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 暗転の下でシーンを読み込む常駐クラス。重ねて読むシーンの読み終わりとロード後の処理まで待ってから暗転を明ける。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SceneLoader : MonoBehaviour
    {
        /// <summary>常駐している唯一のインスタンス。</summary>
        public static SceneLoader Instance { get; private set; }

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

        // 本体の有効化につられて始まった追加の読み込み(重ねる小物シーンなど)。進み具合(0..1、1で完了)を返す関数で持つ
        private readonly List<Func<float>> _additiveLoads = new();

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

        /// <summary>暗転してから <paramref name="sceneName"/> を読み込む。読み込み中に来た呼び出しは無視する。</summary>
        /// <param name="onSceneActivated">
        /// 対象シーンが有効化され、それにつられて始まった追加の読み込み(<see cref="WaitFor"/>)も
        /// 終わったあと(オーバーレイを閉じる前)に一度だけ呼ばれる。
        /// セーブ復元でプレイヤーを保存座標へ配置する等、ロード後処理をここで行う(画面はまだ暗幕の下)。
        /// </param>
        public void LoadScene(string sceneName, Action onSceneActivated = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning($"[SceneLoader] LoadScene ignored (already loading): {sceneName}");
                return;
            }
            // 読めないシーン名だと LoadSceneAsync が null を返し、暗転したまま止まるので先に弾く
            if (!CanLoad(sceneName))
            {
                Debug.LogError(
                    $"[SceneLoader] Build Settings に無いシーンは読み込めません: {sceneName}"
                );
                return;
            }
            StartCoroutine(LoadRoutine(sceneName, onSceneActivated));
        }

        /// <summary>
        /// 本体シーンの読み込みにつられて始まった追加の読み込みを、暗転の下で待つ対象に加える。
        /// 本体の有効化(Awake)の中で呼ぶ(AdditiveScenes)。<paramref name="progress01"/> が 1 を返したら完了とみなす。
        /// </summary>
        public void WaitFor(Func<float> progress01)
        {
            if (progress01 != null)
                _additiveLoads.Add(progress01);
        }

        /// <summary>Build Settings に入っていて読み込めるシーン名か。</summary>
        public static bool CanLoad(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        private IEnumerator LoadRoutine(string sceneName, Action onSceneActivated)
        {
            _isLoading = true;
            // 前の遷移やエディタでの直接再生で登録された読み込みを持ち越さない
            _additiveLoads.Clear();

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
            while (AdditiveProgress01() < 1f)
            {
                _overlay?.SetProgress(mainShare + _additiveLoadShare * AdditiveProgress01());
                yield return null;
            }
            _additiveLoads.Clear();
            _overlay?.SetProgress(1f);
            Application.backgroundLoadingPriority = loadingPriority;

            // 重ねるシーンまで揃ったあと・暗転を明ける前にロード後処理(プレイヤー配置など)を実行する。
            onSceneActivated?.Invoke();

            if (_overlay != null)
                yield return _overlay.HideCoroutine(_fadeSeconds);

            _isLoading = false;
        }

        /// <summary>追加の読み込みの進み具合の平均(何も無ければ 1)。</summary>
        private float AdditiveProgress01()
        {
            if (_additiveLoads.Count == 0)
                return 1f;
            float sum = 0f;
            foreach (var load in _additiveLoads)
                sum += Mathf.Clamp01(load());
            return sum / _additiveLoads.Count;
        }
    }

    /// <summary>コードから参照するシーン名。</summary>
    public static class SceneNames
    {
        /// <summary>タイトル画面。</summary>
        public const string Title = "Title";

        /// <summary>Title の遷移先の初期値。</summary>
        public const string FieldArea01 = "Field_Area01";
    }
}
