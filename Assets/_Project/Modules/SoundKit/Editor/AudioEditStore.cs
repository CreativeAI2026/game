using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタの元ファイルの保存と編集の記録。素材1つにつき、プロジェクト直下の
    /// AudioEditBackup/（素材のパス）/ に次の3つを置く。
    ///   restore.拡張子 … 初めて編集する直前のファイル。「元に戻す」でこれに戻す
    ///   source.拡張子  … 編集の元にする素材。音量をそろえた素材なら、そろえる前の素材（書き出しの劣化を重ねないため）
    ///   edit.json     … 最後に保存した編集内容と、そのとき書き出したファイルの md5
    /// 今のファイルが最後に書き出したものと違う（差し替えられた）場合は、記録を捨てて新しい素材として扱う。
    /// </summary>
    internal static class AudioEditStore
    {
        public const string BackupFolder = "AudioEditBackup";

        [Serializable]
        private class Record
        {
            public string OutputMd5;
            public AudioEditSettings Settings;
        }

        /// <summary>
        /// 編集の元にする素材の場所と、前回保存した編集内容（無ければ null）を返す。
        /// 前回この画面で保存したファイルのままなら、保存しておいた編集前の素材から作り直す。
        /// </summary>
        public static string ResolveSource(string assetPath, out AudioEditSettings previous)
        {
            if (TryLoadCurrentRecord(assetPath, out Record record))
            {
                previous = record.Settings;
                return SourcePath(assetPath);
            }

            previous = null;
            return AudioLoudnessNormalizer.TryGetNormalizedOriginal(assetPath, out string original)
                ? original
                : Path.GetFullPath(assetPath);
        }

        /// <summary>「元に戻す」で戻せる元ファイルを保存してあるか（今のファイルが前回この画面で保存したもののときだけ）。</summary>
        public static bool CanRestore(string assetPath) => TryLoadCurrentRecord(assetPath, out _);

        /// <summary>
        /// 書き出した renderedPath で素材を上書きし、記録を残す。
        /// 初めての編集（または差し替えられた素材）なら、上書きの前に元ファイルと編集の元の素材を保存する。
        /// </summary>
        public static void Commit(string assetPath, string sourcePath, string renderedPath, AudioEditSettings settings)
        {
            string filePath = Path.GetFullPath(assetPath);
            if (!TryLoadCurrentRecord(assetPath, out _))
            {
                string folder = FolderOf(assetPath);
                // 前の記録（差し替え前の素材のもの）が残っていれば消してから作り直す
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);
                File.Copy(filePath, RestorePath(assetPath));
                File.Copy(sourcePath, SourcePath(assetPath));
            }

            File.Copy(renderedPath, filePath, true);
            var record = new Record { OutputMd5 = AudioLoudnessNormalizer.ComputeMd5(filePath), Settings = settings };
            File.WriteAllText(RecordPath(assetPath), JsonUtility.ToJson(record, true));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>初めて編集する前のファイルに戻し、保存していた元ファイルと記録を消す。</summary>
        public static void Restore(string assetPath)
        {
            File.Copy(RestorePath(assetPath), Path.GetFullPath(assetPath), true);
            Directory.Delete(FolderOf(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// 素材の名前変更・移動に合わせて、保存していた元ファイルと記録を新しいパスの場所へ移す
        /// （記録は素材のパスで探すため、移さないと「元に戻す」や編集の続きができなくなる）。
        /// </summary>
        public static void MoveRecord(string fromAssetPath, string toAssetPath)
        {
            // 記録のあるフォルダだけを移す。フォルダごと動かした場合は、中の素材1つずつについて呼ばれる
            if (!File.Exists(RecordPath(fromAssetPath))) return;

            string from = FolderOf(fromAssetPath);
            string to = FolderOf(toAssetPath);
            if (Directory.Exists(to)) Directory.Delete(to, true);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            Directory.Move(from, to);

            // 拡張子が変わった場合（.wav → .WAV など）でも見つかるよう、中のファイル名を新しい拡張子に合わせる
            RenameByExtension(to, "restore", toAssetPath);
            RenameByExtension(to, "source", toAssetPath);
        }

        private static void RenameByExtension(string folder, string baseName, string assetPath)
        {
            string expected = Path.Combine(folder, baseName + Path.GetExtension(assetPath));
            if (File.Exists(expected)) return;
            string[] candidates = Directory.GetFiles(folder, baseName + ".*");
            if (candidates.Length == 1) File.Move(candidates[0], expected);
        }

        private static bool TryLoadCurrentRecord(string assetPath, out Record record)
        {
            record = null;
            string recordPath = RecordPath(assetPath);
            if (!File.Exists(recordPath) || !File.Exists(SourcePath(assetPath)) || !File.Exists(RestorePath(assetPath)))
                return false;

            record = JsonUtility.FromJson<Record>(File.ReadAllText(recordPath));
            return record != null
                   && record.Settings != null
                   && record.OutputMd5 == AudioLoudnessNormalizer.ComputeMd5(Path.GetFullPath(assetPath));
        }

        private static string FolderOf(string assetPath) => Path.GetFullPath(Path.Combine(BackupFolder, assetPath));
        private static string RestorePath(string assetPath) => Path.Combine(FolderOf(assetPath), "restore" + Path.GetExtension(assetPath));
        private static string SourcePath(string assetPath) => Path.Combine(FolderOf(assetPath), "source" + Path.GetExtension(assetPath));
        private static string RecordPath(string assetPath) => Path.Combine(FolderOf(assetPath), "edit.json");
    }
}
