using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度ログ（PanicSessionLogger）の保存先を決める。
    ///
    /// エディター … プロジェクト直下の Logs/Panic（git 管理外）
    /// ビルド     … 実行ファイルと同じフォルダの PanicLogs。テストする人がフォルダごと圧縮して送れるようにするため。
    ///              書き込めない場所（Program Files など）に置かれた時だけ persistentDataPath/PanicLogs に切り替える。
    /// </summary>
    public static class PanicLogLocation
    {
        private const string BuildFolderName = "PanicLogs";
        private const string GuideFileName = "ログの送り方.txt";

        /// <summary>保存先のフォルダを決めて作る。どこにも書き込めなければ null。</summary>
        public static string PrepareDirectory()
        {
#if UNITY_EDITOR
            string directory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "Panic")
            );
            return CanWrite(directory) ? directory : null;
#else
            // Windows のビルドでは dataPath が「実行ファイルのフォルダ/<名前>_Data」になるので、その親が実行ファイルのフォルダ
            string besideExecutable = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                BuildFolderName
            );
            if (CanWrite(besideExecutable))
                return besideExecutable;

            string fallback = Path.Combine(Application.persistentDataPath, BuildFolderName);
            return CanWrite(fallback) ? fallback : null;
#endif
        }

        /// <summary>保存先のフォルダに、テストする人向けの送り方の説明を置く（既にあれば書き直さない）。</summary>
        public static void WriteGuide(string directory)
        {
            string path = Path.Combine(directory, GuideFileName);
            if (File.Exists(path))
                return;

            const string guide =
                "焦り度ログの送り方\r\n"
                + "\r\n"
                + "1. ゲームを終了してください（終了すると最後まで書き込まれます）。\r\n"
                + "2. このフォルダ（PanicLogs）を丸ごと圧縮（zip）してください。\r\n"
                + "3. 圧縮したファイルを開発者に送ってください。\r\n"
                + "\r\n"
                + "中身:\r\n"
                + "  panic_日付_時刻.csv      … 遊んでいる間の記録（1秒に10回）\r\n"
                + "  panic_日付_時刻_info.txt … 記録した日時・操作機器などの情報\r\n";

            try
            {
                File.WriteAllText(path, guide, new UTF8Encoding(true));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 説明が置けなくても記録自体はできるので、警告だけにする
                Debug.LogWarning(
                    $"[焦り度ログ] 送り方の説明を書けませんでした: {path}\n{e.Message}"
                );
            }
        }

        // フォルダを作り、実際に書き込めるかを試す（作れても書き込みだけ禁止されている場所があるため）
        private static bool CanWrite(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write_test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[焦り度ログ] ここには保存できません: {directory}\n{e.Message}");
                return false;
            }
        }
    }
}
