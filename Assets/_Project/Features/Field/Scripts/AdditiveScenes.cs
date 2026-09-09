using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// このシーンに<b>重ねて読み込む別シーン</b>を宣言する係。小物(机・椅子・棚…)を
    /// 階ごとの別ファイルに分けたまま遊べるようにするためのもの。
    ///
    /// 分けている理由は <b>git の競合を構造的に防ぐ</b>ため — 1F/2F/3F の担当者が別ファイルを
    /// 触るので、同じ .unity を同時に書き換えることがない(documents/PropPlacementWorkflow.md)。
    /// 畳んで1枚にしてしまうと分離が失われるので、<b>畳まずに実行時に重ねる</b>。
    ///
    /// 置き場は生成物 <c>Map</c> の<b>外</b>にすること。<c>Rebuild Field_Area01</c> は Map ルートだけを
    /// 作り直すので、外に置いておけば消えない。
    ///
    /// 対象シーンは <b>Build Settings に登録</b>しておく必要がある(名前で読むため)。
    /// 未登録だと実行時に読み込めないので、その場合は警告を出して先へ進む(小物が出ないだけ)。
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
