using System.IO;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セーブファイル(単一スロット)の置き場所と読み書き。<see cref="SaveService"/> と <see cref="LoadService"/> が使う。
    /// 書きかけのファイルで本体を壊さないよう、一時ファイルに書き切ってから差し替え、
    /// 差し替え前の本体を1世代だけ残して、本体が読めないときの予備にする。
    /// </summary>
    internal static class SaveFile
    {
        private static string _directoryOverride;

        /// <summary>本体のパス。</summary>
        internal static string FilePath =>
            Path.Combine(_directoryOverride ?? Application.persistentDataPath, "save.json");

        private static string TempPath => FilePath + ".tmp";
        private static string BackupPath => FilePath + ".bak";

        /// <summary>
        /// テスト専用: 保存先フォルダを差し替える。null を渡すと persistentDataPath に戻す。
        /// 開発者の手元のセーブをテストが読み書きしないようにするための口。
        /// </summary>
        internal static void OverrideDirectoryForTests(string directory) =>
            _directoryOverride = directory;

        /// <summary>本体か予備があるか。</summary>
        internal static bool Exists() => File.Exists(FilePath) || File.Exists(BackupPath);

        /// <summary>一時ファイルに書き切ってから本体と差し替える(差し替え前の本体は予備に残す)。</summary>
        internal static void Write(string json)
        {
            File.WriteAllText(TempPath, json);
            if (File.Exists(FilePath))
                File.Replace(TempPath, FilePath, BackupPath);
            else
                File.Move(TempPath, FilePath);
        }

        /// <summary>本体を読み、読めなければ予備を読む。どちらも無い・壊れていれば null。</summary>
        internal static SaveData Read()
        {
            if (TryRead(FilePath, out var data))
                return data;
            if (TryRead(BackupPath, out data))
            {
                Debug.LogWarning("[SaveFile] セーブが読めないため、1つ前のセーブから復元します。");
                return data;
            }
            if (Exists())
                Debug.LogError("[SaveFile] セーブデータが壊れていて読み込めません。");
            return null;
        }

        private static bool TryRead(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path))
                return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (System.Exception e) when (e is System.ArgumentException || e is IOException)
            {
                Debug.LogWarning($"[SaveFile] 読み込みに失敗しました: {path}\n{e.Message}");
                return false;
            }
            return data != null;
        }
    }
}
