using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// このシーンに重ねて読み込む別シーンを宣言する。階ごとの小物シーンを畳まずに実行時に重ね、
    /// 担当者ごとにファイルを分けたまま git 競合を防ぐ。生成物 <c>Map</c> の外に置くこと(Rebuild で消えない)。
    /// 対象シーンは Build Settings 登録が必要で、未登録なら警告して先へ進む(小物が出ないだけ)。
    /// </summary>
    public sealed class AdditiveScenes : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("重ねて読むシーン名(拡張子なし)。Build Settings に登録が必要")]
        private string[] _sceneNames = System.Array.Empty<string>();

        [SerializeField]
        [Tooltip("エディタで既に重ねて開いているシーンは読み直さない(作業中の状態を壊さないため)")]
        private bool _skipAlreadyLoaded = true;

        private void Awake()
        {
            foreach (var name in _sceneNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (_skipAlreadyLoaded && IsLoaded(name))
                    continue;

                if (!CanLoad(name))
                {
                    Debug.LogWarning(
                        $"[AdditiveScenes] シーン '{name}' を読めません。"
                            + "Build Settings に登録されているか確認してください(小物は出ません)。"
                    );
                    continue;
                }

                SceneManager.LoadScene(name, LoadSceneMode.Additive);
            }
        }

        private static bool IsLoaded(string name)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == name)
                    return true;
            return false;
        }

        /// <summary>Build Settings に載っているか。載っていないと LoadScene が例外を投げる。</summary>
        private static bool CanLoad(string name)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                var start = path.LastIndexOf('/') + 1;
                var end = path.LastIndexOf('.');
                if (
                    end > start
                    && string.CompareOrdinal(path, start, name, 0, end - start) == 0
                    && end - start == name.Length
                )
                    return true;
            }
            return false;
        }

        /// <summary>いま宣言されているシーン名(Editor ツールからの確認用)。</summary>
        public IReadOnlyList<string> SceneNames => _sceneNames;
    }
}
