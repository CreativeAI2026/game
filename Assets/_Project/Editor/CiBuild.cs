using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// CI(GameCI unity-builder)の buildMethod から呼ぶビルド入口。
    /// buildMethod を指定しないと GameCI が自前のビルドスクリプトを Assets/Editor に差し込み、
    /// それが解析ルール(UNT)と警告のエラー化(csc.rsp の -warnaserror+)に引っかかってコンパイルが落ちるため、自前で持つ。
    /// </summary>
    public static class CiBuild
    {
        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            string path = GetArg(args, "-customBuildPath");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("-customBuildPath が指定されていません");

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings
                    .scenes.Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray(),
                locationPathName = path,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            UnityEngine.Debug.Log(
                $"[CiBuild] {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize} bytes"
            );
            bool succeeded = summary.result == BuildResult.Succeeded;
            // GameCI はログに "Build succeeded!" があるかで成否を判定する(無いと成功しても失敗扱いになる)
            if (succeeded)
                UnityEngine.Debug.Log("Build succeeded!");
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static string GetArg(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
