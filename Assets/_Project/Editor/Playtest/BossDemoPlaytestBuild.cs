using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// TBossDemo と MidBossDemo だけを入れた「ボス体験用ビルド」を書き出す。
    /// シーン一覧はここで持ち、共有の Build Settings(他の人のフィールドのシーンが並ぶ・CI もこれでビルドする)には触れない。
    /// 開発ビルドにはしない(開発ビルドだと焦り度の CSV 記録やクラゲのデバッグ操作が動き出すため)。
    /// ビルド中だけ <see cref="BossDemoPlaytestSceneStripper"/> がデバッグ用の部品をシーンから外す。
    /// </summary>
    public static class BossDemoPlaytestBuild
    {
        // 先頭のシーンが起動時のシーンになる。
        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Battle/TBossDemo.unity",
            "Assets/_Project/Scenes/Battle/MidBossDemo.unity",
        };

        private const string DefaultOutputFolder = "Build_Windows/BossDemoPlaytest";
        private const string ExecutableName = "BossDemo.exe";

        /// <summary>このビルドの最中か。<see cref="BossDemoPlaytestSceneStripper"/> が他のビルドで部品を外さないために見る。</summary>
        internal static bool IsBuilding { get; private set; }

        [MenuItem("Tools/CreativeAI/Build/ボス体験用ビルドを書き出す")]
        private static void BuildFromMenu()
        {
            foreach (string path in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogError($"[BossDemoPlaytestBuild] シーンが見つかりません: {path}");
                    return;
                }
            }

            string folder = EditorUtility.SaveFolderPanel(
                "ボス体験用ビルドの書き出し先",
                DefaultOutputFolder,
                ""
            );
            if (string.IsNullOrEmpty(folder))
                return;

            Build(Path.Combine(folder, ExecutableName));
        }

        private static void Build(string executablePath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report;
            IsBuilding = true;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                IsBuilding = false;
            }

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(
                    $"[BossDemoPlaytestBuild] 書き出しました: {executablePath}({summary.totalSize / (1024 * 1024)} MB)"
                );
                EditorUtility.RevealInFinder(executablePath);
            }
            else
            {
                Debug.LogError(
                    $"[BossDemoPlaytestBuild] 失敗しました: {summary.result}(エラー {summary.totalErrors} 件)"
                );
            }
        }
    }
}
